/*
 * Advanced Custom Blips
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
using System.Runtime.CompilerServices;

using LemonUI;
using LemonUI.Menus;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using GTA;
using GTA.UI;
using GTA.Math;
using GTA.Native;

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
            return (double)u / (double)(1UL << 53);
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
    }

    public sealed class Logger
    {
        private static readonly string _logFilePath = Path.Combine(BaseDirectory, "Realistic Vehicle Radio Log.txt");
        private static readonly StreamWriter _logWriter;

        static Logger()
        {
            EnsureDirectoryExist(_logFilePath);

            _logWriter = new StreamWriter(new FileStream(
            _logFilePath, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, true))
            {
                AutoFlush = true
            };
        }

        public static void DisplayAndLogError(Exception ex, string contextInfo = null)
        {
            string methodName = ex.TargetSite?.Name ?? "Unknown Method";  // Get method name, if not possible then assign Unknown Method value
            string innerMessage = ex.InnerException?.Message != null ? $"\nInner Exception: {ex.InnerException.Message}" : string.Empty;
            string message = $"~r~Error in {methodName}: {ex.Message}{innerMessage}";

            // Add context details if available. Was added to make it easyier for the end user to identify the problem without the need to read the whole exception.
            if (!string.IsNullOrEmpty(contextInfo))
            {
                message += $"\nContext: {contextInfo}";
            }

            Notification.PostTicker(message, true);
            try
            {
                // Ensure directory exist, create one if it does not.
                EnsureDirectoryExist(_logFilePath);

                _logWriter.WriteLine($"{DateTime.Now}: {methodName}: {ex}");
                if (contextInfo != null)
                {
                    _logWriter.WriteLine($"Context: {contextInfo}");
                }
                _logWriter.WriteLine($"Stack Trace: {ex.StackTrace}");
                _logWriter.WriteLine();
                _logWriter.WriteLine(new string('#', 80)); // Separator for readability
                _logWriter.WriteLine();

            }
            catch (UnauthorizedAccessException logEx)
            {
                // Handle potential permission issues, e.g., read-only attribute
                // You could attempt to remove read-only attribute if needed before retrying
                FileAttributes attributes = File.GetAttributes(_logFilePath);
                if (File.Exists(_logFilePath) && (attributes & FileAttributes.ReadOnly) != 0)
                {
                    attributes &= ~FileAttributes.ReadOnly;
                    File.SetAttributes(_logFilePath, attributes);
                    // Retry writing after clearing the attribute
                    _logWriter.WriteLine($"{DateTime.Now}: {methodName}: {ex}");
                    if (contextInfo != null)
                    {
                        _logWriter.WriteLine($"Context: {contextInfo}");
                    }
                    _logWriter.WriteLine($"Stack Trace: {ex.StackTrace}");
                    _logWriter.WriteLine();
                    _logWriter.WriteLine(new string('#', 80)); // Separator for readability
                    _logWriter.WriteLine();
                    return; // Success after fixing attributes
                }
                else
                {
                    // Could not fix or wasn't a read-only issue, re-throw
                    Notification.PostTicker($"~r~Failed to log exception (access denied): {logEx.Message}", true);
                    throw;
                }
            }
            catch (DirectoryNotFoundException)
            {
                Notification.PostTicker($"Directory not found: {Path.GetDirectoryName(_logFilePath)}", true);
            }
            catch (IOException logEx)
            {
                Notification.PostTicker($"~r~Failed to log exception (I/O error): {logEx.Message}", true);
            }
            catch (Exception logEx)
            {
                Notification.PostTicker($"~r~Failed to log exception: {logEx.Message}", true);
            }
        }
        public static void EnsureDirectoryExist(string filePath)
        {
            try
            {
                string directoryPath = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }
            }
            catch (PathTooLongException ex)
            {
                DisplayAndLogError(ex, $"Error ensuring directory exists for path (path too long): {filePath}");
            }
            catch (IOException ex)
            {
                DisplayAndLogError(ex, $"Error ensuring directory exists for path (I/O error): {filePath}");
            }
            catch (UnauthorizedAccessException ex)
            {
                // Handle potential permission issues, e.g., read-only attribute
                // You could attempt to remove read-only attribute if needed before retrying
                FileAttributes attributes = File.GetAttributes(filePath);
                if (File.Exists(filePath) && (attributes & FileAttributes.ReadOnly) != 0)
                {
                    attributes &= ~FileAttributes.ReadOnly;
                    File.SetAttributes(filePath, attributes);
                    // Retry writing after clearing the attribute
                    Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                    return; // Success after fixing attributes
                }
                else
                {
                    // Could not fix or wasn't a read-only issue, re-throw
                    DisplayAndLogError(ex, $"Error ensuring directory exists for path (access denied): {filePath}. Tried fixing it but failed.");
                    throw;
                }
            }
            catch (Exception ex)
            {
                DisplayAndLogError(ex, $"Error ensuring directory exists for path: {filePath}");
            }
        }
    }

    public class Vector3Converter : JsonConverter
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


            if (Function.Call<int>(Hash.UPDATE_ONSCREEN_KEYBOARD) == 1)
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
    }
}