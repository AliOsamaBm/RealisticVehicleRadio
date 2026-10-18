/*
 * Realistic Vehicle Radio
 * Copyright (C) 2026 Ali Osama
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 2 of the License.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
 *
 * See the GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */

using System;
using System.IO;
using System.Windows.Forms;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

using LemonUI;
using LemonUI.Menus;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using GTA;
using GTA.UI;
using GTA.Math;
using GTA.Native;
using Screen = GTA.UI.Screen;

namespace RealisticVehicleRadio
{
    public static class Helpers
    {
        public static readonly string[] KeysNames = Enum.GetNames(typeof(Keys));
        public static readonly string BaseDirectory = Path.Combine("scripts", "Realistic Vehicle Radio");

        public sealed class ValidFunctions
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsValidPed(Ped ped)
            {
                return ped != null && ped.Exists();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsValidVehicle(Vehicle vehicle)
            {
                return vehicle != null && vehicle.Exists();
            }
        }

        public sealed class MathFunctions
        {
            private static uint _rng = 1u; // default non-zero

            // ===========================================
            // Pseudo-Random Number Generator (PRNG)
            // ===========================================
            public static void Seed(uint seed)
            {
                _rng = seed == 0 ? 1u : seed;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static int NextInt(int min, int max)
            {
                uint x;
                uint range = (uint)(max - min);

                if (range == 0)
                {
                    return min;
                }

                uint limit = uint.MaxValue - (uint.MaxValue % range);

                do
                {
                    x = NextUInt();
                }
                while (x >= limit);

                return (int)(x % range) + min;
            }

            // --- double RNG ---
            // Produces a uniformly distributed double in [0,1).
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static double NextDouble()
            {
                // Build a 64-bit value from two 32-bit NextUInt() calls.
                // Then shift right by 11 to keep 53 random bits (the precision of IEEE-754 double).
                // Result u is in [0, 2^53).
                ulong hi = NextUInt();
                ulong lo = NextUInt();
                ulong u = (hi << 32) | lo;
                u >>= 11; // keep top 53 bits -> value in [0, 2^53)

                // divide by 2^53 to get a double in [0,1)
                return u / (double)(1UL << 53);
            }

            // Faster variant that skips min<=max validation (use only when caller guarantees valid range)
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static double NextDouble(double min, double max)
            {
                double frac = NextDouble(); // in [0,1)
                return min + frac * (max - min);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static uint NextUInt()
            {
                uint x = _rng;

                x ^= x << 13;
                x ^= x >> 17;
                x ^= x << 5;

                _rng = x == 0 ? 0x9E3779B1u : x;
                return _rng;
            }

            // ===========================================
            // 2D Perlin Noise
            // ===========================================
            public static float PerlinNoise(float x, float y)
            {
                int x0 = (int)Math.Floor(x);
                int y0 = (int)Math.Floor(y);

                float fx = x - x0;
                float fy = y - y0;

                float u = Fade(fx);
                float v = Fade(fy);

                float top = Lerp(
                    Grad(Hash(x0, y0) & 255, fx, fy),
                    Grad(Hash(x0 + 1, y0) & 255, fx - 1f, fy),
                    u
                );

                float bottom = Lerp(
                    Grad(Hash(x0, y0 + 1) & 255, fx, fy - 1f),
                    Grad(Hash(x0 + 1, y0 + 1) & 255, fx - 1f, fy - 1f),
                    u
                );

                return Lerp(top, bottom, v);
            }
            private static float Fade(float t)
            {
                // 6t^5 - 15t^4 + 10t^3
                return t * t * t * (t * (t * 6 - 15) + 10);
            }
            private static float Lerp(float a, float b, float t)
            {
                return a + t * (b - a);
            }
            private static int Hash(int x, int y)
            {
                // Simple hash function
                int h = x * 1619 + y * 31337;
                h = (h ^ (h >> 16)) * 0x45d9f3b;
                h = (h ^ (h >> 16)) * 0x45d9f3b;
                h ^= (h >> 16);
                return h;
            }
            private static float Grad(int hash, float x, float y)
            {
                // Convert low 4 bits of hash code into 12 gradient directions
                int h = hash & 15;
                float u = h < 8 ? x : y;
                float v = h < 4 ? y : h == 12 || h == 14 ? x : 0;
                return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
            }

            // ===========================================
            // Clamp 
            // ===========================================
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static float Clamp(float value, float min, float max)
            {
                return value < min ? min : (value > max ? max : value);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static int Clamp(int value, int min, int max)
            {
                return value < min ? min : (value > max ? max : value);
            }
        }

        public sealed class Logger
        {
            private static readonly string _logFilePath = Path.Combine(BaseDirectory, "Realistic Vehicle Radio Log.txt");

            private static readonly string _separator = new string('#', 80);

            // Lazy writer: null if it could not be created. Never throws at type-init.
            private static readonly object _writerLock = new object();
            private static StreamWriter _logWriter;
            private static bool _writerInitialized;

            private static StreamWriter TryGetWriterNoLock()
            {
                if (_writerInitialized)
                {
                    return _logWriter;
                }

                _writerInitialized = true;

                try
                {
                    string dir = Path.GetDirectoryName(_logFilePath);

                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    _logWriter = new StreamWriter(new FileStream(
                        _logFilePath, FileMode.Append, FileAccess.Write,
                        FileShare.Read, 4096, useAsync: true))
                    {
                        AutoFlush = true
                    };
                }
                catch
                {
                    _logWriter = null;
                }

                return _logWriter;
            }

            public static void DisplayAndLogError(Exception ex, string contextInfo = null)
            {
                if (ex == null)
                {
                    return;
                }

                string methodName = ex.TargetSite?.Name ?? "Unknown Method";
                string userMessage = BuildUserMessage(ex, methodName, contextInfo);

                GUI.ShowContextualMessage(userMessage);
                WriteEntry(ex, methodName, contextInfo);
            }

            private static string BuildUserMessage(Exception ex, string methodName, string contextInfo)
            {
                string innerMessage = ex.InnerException?.Message != null
                    ? $"\nInner Exception: {ex.InnerException.Message}"
                    : string.Empty;

                string message = $"~r~Error in {methodName}: {ex.Message}{innerMessage}";

                if (!string.IsNullOrEmpty(contextInfo))
                {
                    message += $"\nContext: {contextInfo}";
                }

                return message;
            }

            private static void WriteEntry(Exception ex, string methodName, string contextInfo)
            {
                lock (_writerLock)
                {
                    StreamWriter writer = TryGetWriterNoLock();

                    if (writer == null)
                    {
                        return;
                    }

                    try
                    {
                        writer.WriteLine($"{DateTime.Now}: {methodName}: {ex}");

                        if (contextInfo != null)
                        {
                            writer.WriteLine($"Context: {contextInfo}");
                        }

                        writer.WriteLine($"Stack Trace: {ex.StackTrace}");
                        writer.WriteLine();
                        writer.WriteLine(_separator);
                        writer.WriteLine();
                    }
                    catch
                    {
                        // Writer failed (disk full, file deleted mid-run, etc.).
                        // Drop the writer so the next call attempts a fresh one.
                        try
                        {
                            _logWriter?.Dispose();
                            _logWriter = null;
                            _writerInitialized = false;
                        }
                        catch
                        {
                            // Ignore.
                        }
                    }
                }
            }

            public static bool EnsureDirectoryExist(string filePath)
            {
                try
                {
                    string directoryPath = Path.GetDirectoryName(filePath);

                    if (string.IsNullOrEmpty(directoryPath))
                    {
                        return false;
                    }

                    if (Directory.Exists(directoryPath))
                    {
                        return true;
                    }

                    Directory.CreateDirectory(directoryPath);
                    return true;
                }
                catch (UnauthorizedAccessException)
                {
                    return FileOperations.TryClearReadOnlyDirectory(filePath);
                }
                catch
                {
                    return false;
                }
            }
        }

        public sealed class Vector3Converter : JsonConverter
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(Vector3);
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null)
                {
                    return Vector3.Zero;
                }

                JObject jo = JObject.Load(reader);
                float x = jo["X"]?.ToObject<float>() ?? 0f;
                float y = jo["Y"]?.ToObject<float>() ?? 0f;
                float z = jo["Z"]?.ToObject<float>() ?? 0f;
                return new Vector3(x, y, z);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                Vector3 v = (Vector3)value;
                writer.WriteStartObject();
                writer.WritePropertyName("X");
                writer.WriteValue(v.X);
                writer.WritePropertyName("Y");
                writer.WriteValue(v.Y);
                writer.WritePropertyName("Z");
                writer.WriteValue(v.Z);
                writer.WriteEndObject();
            }
        }

        public sealed class GUI
        {
            private static readonly ConcurrentQueue<string> _mainThreadMessages = new ConcurrentQueue<string>();

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static NativeMenu CreateAndRegisterMenu(ObjectPool menuPool, string title, string subtitle)
            {
                NativeMenu menu = new NativeMenu(title, subtitle)
                {
                    KeepNameCasing = true
                };

                menuPool.Add(menu);
                return menu;
            }

            public static string PromptUserForInput(bool isNumber = false)
            {
                Function.Call(Hash.DISPLAY_ONSCREEN_KEYBOARD, 1, "", "", "", "", "", "", 30);

                int state;
                do
                {
                    Script.Wait(0);
                    state = Function.Call<int>(Hash.UPDATE_ONSCREEN_KEYBOARD);
                } while (state == 0);


                if (state == 1)
                {
                    string result = Function.Call<string>(Hash.GET_ONSCREEN_KEYBOARD_RESULT);

                    // Normalize decimal separator
                    return isNumber ? result?.Replace(',', '.') : result;
                }

                return null;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static NativeCheckboxItem CreateCheckboxItem(string title, bool defaultValue, string description = "")
            {
                NativeCheckboxItem item = new NativeCheckboxItem(title, defaultValue)
                {
                    Description = description
                };

                return item;
            }

            public static NativeListItem<string> CreateEnumListItem<TEnum>(string title, bool indexIncluded, int defaultIndex = 0, string description = null) where TEnum : Enum
            {
                Array values = Enum.GetValues(typeof(TEnum));
                int count = values.Length;

                string[] items = new string[count];

                if (indexIncluded)
                {
                    for (int i = 0; i < count; i++)
                    {
                        TEnum value = (TEnum)values.GetValue(i);
                        items[i] = $"{value} ({Convert.ToInt32(value)})";
                    }
                }
                else
                {
                    for (int i = 0; i < count; i++)
                    {
                        items[i] = values.GetValue(i).ToString();
                    }
                }

                NativeListItem<string> item = new NativeListItem<string>(title, items)
                {
                    SelectedIndex = defaultIndex
                };

                if (!string.IsNullOrWhiteSpace(description))
                {
                    item.Description = description;
                }

                return item;
            }

            // New in V1.3.0: Added a helper that switches between help text and subtitle text based on menu visibility
            /// <summary>
            /// Shows a contextual message to the player.
            /// Uses ShowSubtitle when any menu is visible (to avoid overlap with menu UI),
            /// otherwise uses ShowHelpText (less intrusive, top-left corner).
            /// </summary>
            /// <param name="message">The message to display (supports GTA color codes like ~b~, ~g~, etc.)</param>
            /// <param name="duration">Duration in milliseconds.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void ShowContextualMessage(string message, int duration)
            {
                if (Menus.UiMenuPool.AreAnyVisible)
                {
                    Screen.ShowSubtitle(message, duration < 1000 ? 1000 : duration);
                }
                else
                {
                    Screen.ShowHelpText(message, duration, false, false);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void ShowContextualMessage(string message)
            {
                _mainThreadMessages.Enqueue(message);
            }

            public static void ProcessMessages()
            {
                while (!Menus.WaitingForKeyAssignment && _mainThreadMessages.TryDequeue(out string message))
                {
                    Notification.PostTicker(message, true);
                }
            }
        }

        private static readonly Model _driverModel = new Model(PedHash.RaceOrg01SMM);

        public static string GetPlayerRadioStationName()
        {
            int stationIndex = Function.Call<int>(Hash.GET_PLAYER_RADIO_STATION_INDEX);

            if (stationIndex < 0)
            {
                return null;
            }

            string stationName = Function.Call<string>(Hash.GET_RADIO_STATION_NAME, stationIndex);

            return string.IsNullOrWhiteSpace(stationName) ? null : stationName;
        }

        /// <summary>
        /// Spawns an invisible ped in the driver seat to trick the game
        /// into thinking the vehicle has a driver during radio unmute/mute.
        /// </summary>
        public static Ped SpawnInvisibleDriverPedFor(Vehicle vehicle)
        {
            try
            {
                if (!ValidFunctions.IsValidVehicle(vehicle) || !vehicle.IsSeatFree(VehicleSeat.Driver))
                {
                    return null;
                }

                Ped ped = vehicle.CreatePedOnSeat(VehicleSeat.Driver, _driverModel);
                if (!ValidFunctions.IsValidPed(ped))
                {
                    return null;
                }

                ped.IsVisible = false;
                ped.CanBeTargetted = false;

                // Ensure that the temp ped doesn't react whatsoever
                Function.Call(Hash.SET_BLOCKING_OF_NON_TEMPORARY_EVENTS, ped.Handle, true);
                Function.Call(Hash.TASK_SET_BLOCKING_OF_NON_TEMPORARY_EVENTS, ped.Handle, true);

                return ped;
            }
            catch
            {
                return null;
            }
        }
    }
}