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
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using static RealisticVehicleRadio.Helpers;

namespace RealisticVehicleRadio
{
    public static class FileOperations
    {
        private static readonly string _configFilePath = Path.Combine(BaseDirectory, "Realistic Vehicle Radio Settings.json");
        private static readonly string _tunnelsFilePath = Path.Combine(BaseDirectory, "Realistic Vehicle Radio Tunnels.json");

        // Concurrency protection: Semaphores serialize access to each file independently
        private static readonly SemaphoreSlim _configLock = new SemaphoreSlim(1, 1);
        private static readonly SemaphoreSlim _tunnelsLock = new SemaphoreSlim(1, 1);

        // ==========================================
        // CACHED JSON SETTINGS (Reduces Class Coupling & Allocations)
        // ==========================================
        private static readonly JsonSerializerSettings _defaultSettings = new JsonSerializerSettings
        {
            Converters = { new StringEnumConverter(), new Vector3Converter() },
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        };

        private static readonly JsonSerializerSettings _comparisonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Ignore,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            Converters = { new StringEnumConverter(), new Vector3Converter() }
        };

        // ==============================================================
        // SAFE IO HELPERS
        // ==============================================================
        private static async Task SafeWriteAllTextNoLockAsync(string filePath, string content)
        {
            string tempPath = filePath + ".tmp";
            string backupPath = filePath + ".bak";

            // Clean up any stale temp files from previous crashes
            if (File.Exists(tempPath))
            {
                try 
                { 
                    File.Delete(tempPath); 
                } 
                catch 
                { 
                    /* Ignore */ 
                }
            }

            // 1. Write to a temporary file
            using (FileStream fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            using (StreamWriter sw = new StreamWriter(fs))
            {
                await sw.WriteAsync(content);
                await sw.FlushAsync();
                await fs.FlushAsync();

                // Force OS to flush to physical disk for power-loss durability without blocking the thread
                await Task.Run(() => fs.Flush(true));
            }

            // 2. Atomically replace the original file
            // Explicitly delete old backup to prevent File.Replace IOExceptions if it already exists
            if (File.Exists(backupPath))
            {
                try 
                { 
                    File.Delete(backupPath); 
                } 
                catch 
                { 
                    /* Ignore */ 
                }
            }

            // Atomically replace
            if (File.Exists(filePath))
            {
                // Replaces the file and creates a .bak backup of the previous version
                File.Replace(tempPath, filePath, backupPath);
            }
            else
            {
                // If the file doesn't exist yet, just move the temp file to the target path
                File.Move(tempPath, filePath);
            }
        }

        private static async Task<string> ReadAllTextNoLockAsync(string filePath)
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true))
            using (StreamReader sr = new StreamReader(fs))
            {
                return await sr.ReadToEndAsync();
            }
        }

        // ================================================================
        // GENERIC FALLBACK LOADER (Fixes DRY & Cyclomatic Complexity)
        // ================================================================
        private static async Task<T> LoadWithFallbackAsync<T>(string mainFilePath, string backupFilePath, string fileType, Func<T> defaultFactory) where T : class
        {
            Logger.EnsureDirectoryExist(mainFilePath);
            bool didComeFromException = false;

            // 1. Try main file
            if (File.Exists(mainFilePath))
            {
                try
                {
                    string json = await ReadAllTextNoLockAsync(mainFilePath);
                    T result = JsonConvert.DeserializeObject<T>(json, _defaultSettings);
                    if (result != null)
                    {
                        return result;
                    }
                }
                catch (Exception ex)
                {
                    Logger.DisplayAndLogError(ex, $"Main {fileType} file corrupted. Attempting to load backup...");
                    didComeFromException = true;
                }
            }

            // 2. Fallback to backup file
            if (File.Exists(backupFilePath))
            {
                try
                {
                    string json = await ReadAllTextNoLockAsync(backupFilePath);
                    T result = JsonConvert.DeserializeObject<T>(json, _defaultSettings);
                    if (result != null)
                    {
                        if (didComeFromException)
                        {
                            GUI.ShowContextualMessage($"~g~Backup {fileType} file successfully loaded!");
                        }
                        return result;
                    }
                }
                catch (Exception ex)
                {
                    Logger.DisplayAndLogError(ex, $"Backup {fileType} file is also corrupted.");
                }
            }

            // 3. If both fail, create default
            return defaultFactory();
        }

        // ==========================================
        // CONFIGURATION METHODS
        // ==========================================
        private static Task<Configuration> LoadConfigurationNoLockAsync()
        {
            return LoadWithFallbackAsync(
                _configFilePath,
                _configFilePath + ".bak",
                "configuration",
                () => new Configuration()
            );
        }

        public static async Task SaveConfigurationAsync(Configuration config)
        {
            await _configLock.WaitAsync();
            try
            {
                await SaveConfigurationNoLockAsync(config);
            }
            finally
            {
                _configLock.Release();
            }
        }

        private static async Task SaveConfigurationNoLockAsync(Configuration config)
        {
            string json = JsonConvert.SerializeObject(config, _defaultSettings);
            Logger.EnsureDirectoryExist(_configFilePath);
            await SafeWriteAllTextNoLockAsync(_configFilePath, json);
        }

        public static async Task<Configuration> LoadConfigurationAsync()
        {
            await _configLock.WaitAsync();
            try 
            { 
                return await LoadConfigurationNoLockAsync(); 
            }
            finally 
            { 
                _configLock.Release(); 
            }
        }

        // ==========================================
        // TUNNELS METHODS
        // ==========================================
        private static Task<List<Tunnel>> LoadTunnelsNoLockAsync()
        {
            return LoadWithFallbackAsync(
                _tunnelsFilePath,
                _tunnelsFilePath + ".bak",
                "tunnels",
                () => new List<Tunnel>()
            );
        }

        private static async Task SaveTunnelsNoLockAsync(List<Tunnel> tunnels)
        {
            string json = JsonConvert.SerializeObject(tunnels, _defaultSettings);
            Logger.EnsureDirectoryExist(_tunnelsFilePath);
            await SafeWriteAllTextNoLockAsync(_tunnelsFilePath, json);
        }

        public static async Task<List<Tunnel>> LoadTunnelsAsync()
        {
            await _tunnelsLock.WaitAsync();
            try 
            { 
                return await LoadTunnelsNoLockAsync(); 
            }
            finally 
            { 
                _tunnelsLock.Release(); 
            }
        }

        public static async Task SaveTunnelsAsync(List<Tunnel> tunnels)
        {
            await _tunnelsLock.WaitAsync();
            try 
            { 
                await SaveTunnelsNoLockAsync(tunnels); 
            }
            finally 
            { 
                _tunnelsLock.Release(); 
            }
        }

        // ==========================================================
        // SECTION SAVE (Read-Modify-Write strictly under lock)
        // ==========================================================
        /// <summary>
        /// Compares a specific settings section with the one in the JSON file. 
        /// If differences are found, updates the JSON file. If not, does nothing.
        /// </summary>
        public static async Task SaveSettingsSectionAsync<T>(T newSettings, Func<Configuration, T> getter, Action<Configuration, T> setter)
        {
            // Acquire lock for the ENTIRE transaction to prevent read-modify-write races
            await _configLock.WaitAsync();
            try
            {
                Configuration currentConfig = await LoadConfigurationNoLockAsync();
                T currentSettings = getter(currentConfig);

                string currentJson = JsonConvert.SerializeObject(currentSettings, _comparisonSettings);
                string newJson = JsonConvert.SerializeObject(newSettings, _comparisonSettings);

                if (currentJson != newJson)
                {
                    setter(currentConfig, newSettings);
                    await SaveConfigurationNoLockAsync(currentConfig);
                    GUI.ShowContextualMessage("~g~Settings saved successfully.");
                }
                else
                {
                    GUI.ShowContextualMessage("~y~No changes detected. Settings not saved.");
                }
            }
            catch (Exception ex)
            {
                Logger.DisplayAndLogError(ex, "Failed to save settings section.");
            }
            finally
            {
                _configLock.Release();
            }
        }

        public static bool TryClearReadOnlyDirectory(string filePath)
        {
            try
            {
                string directoryPath = Path.GetDirectoryName(filePath);

                if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
                {
                    return false;
                }

                FileAttributes attributes = File.GetAttributes(directoryPath);

                if ((attributes & FileAttributes.ReadOnly) != 0)
                {
                    attributes &= ~FileAttributes.ReadOnly;
                    File.SetAttributes(directoryPath, attributes);
                    Directory.CreateDirectory(directoryPath);
                    return true;
                }
            }
            catch
            {
                // Ignore and fall through.
            }

            return false;
        }
    }
}