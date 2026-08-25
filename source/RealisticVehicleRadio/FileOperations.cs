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
using System.Collections.Generic;

using static Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

using GTA.UI;

public static class FileOperations
{
    private static readonly string _configFilePath = Path.Combine(BaseDirectory, "Realistic Vehicle Radio Settings.json");
    private static readonly string _tunnelsFilePath = Path.Combine(BaseDirectory, "Realistic Vehicle Radio Tunnels.json");

    // ==========================================
    // SAFE IO HELPER (Prevents Power-Loss Corruption)
    // ==========================================
    private static void SafeWriteAllText(string filePath, string content)
    {
        string tempPath = filePath + ".tmp";
        string backupPath = filePath + ".bak";

        // Clean up any stale temp files from previous crashes
        if (File.Exists(tempPath))
        {
            try { File.Delete(tempPath); } catch { /* Ignore permission/delete errors */ }
        }

        // 1. Write to a temporary file
        using (FileStream fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        using (StreamWriter sw = new StreamWriter(fs))
        {
            sw.Write(content);
            sw.Flush();
            // Force the OS to write data to the physical disk (crucial for power-loss protection)
            fs.Flush(true);
        }

        // 2. Atomically replace the original file
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

    public static Configuration LoadConfiguration()
    {
        Logger.EnsureDirectoryExist(_configFilePath);

        JsonSerializerSettings settings = new JsonSerializerSettings
        {
            Converters = {
                new StringEnumConverter(),
                new Vector3Converter()
            },
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        };

        // 1. Try main file
        if (File.Exists(_configFilePath))
        {
            try
            {
                string json = File.ReadAllText(_configFilePath);
                Configuration config = JsonConvert.DeserializeObject<Configuration>(json, settings);
                if (config != null)
                {
                    return config;
                }
            }
            catch (Exception ex)
            {
                Logger.DisplayAndLogError(ex, "Main configuration file corrupted. Attempting to load backup...");
            }
        }

        // 2. Fallback to backup file
        string backupPath = _configFilePath + ".bak";
        if (File.Exists(backupPath))
        {
            try
            {
                string json = File.ReadAllText(backupPath);
                Configuration config = JsonConvert.DeserializeObject<Configuration>(json, settings);
                if (config != null)
                {
                    return config;
                }
            }
            catch (Exception ex)
            {
                Logger.DisplayAndLogError(ex, "Backup configuration file is also corrupted.");
            }
        }

        // 3. If both fail, create default
        Configuration defaultConfig = new Configuration();
        SaveConfiguration(defaultConfig);
        return defaultConfig;
    }

    public static void SaveConfiguration(Configuration config)
    {
        try
        {
            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                Converters = {
                    new StringEnumConverter(),
                    new Vector3Converter()
                },
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            };

            string json = JsonConvert.SerializeObject(config, settings);
            Logger.EnsureDirectoryExist(_configFilePath);
            SafeWriteAllText(_configFilePath, json);
        }
        catch (Exception ex)
        {
            Logger.DisplayAndLogError(ex, "Failed to save configuration.");
        }
    }

    public static List<Tunnel> LoadTunnels()
    {
        Logger.EnsureDirectoryExist(_tunnelsFilePath);

        JsonSerializerSettings settings = new JsonSerializerSettings
        {
            Converters = { new StringEnumConverter(), new Vector3Converter() },
            Formatting = Formatting.Indented
        };

        // 1. Try to load from the main file
        if (File.Exists(_tunnelsFilePath))
        {
            try
            {
                string json = File.ReadAllText(_tunnelsFilePath);
                var tunnels = JsonConvert.DeserializeObject<List<Tunnel>>(json, settings);
                if (tunnels != null)
                {
                    return tunnels;
                }
            }
            catch (Exception ex)
            {
                Logger.DisplayAndLogError(ex, "Main tunnels file is corrupted or unreadable. Attempting to load backup...");
            }
        }

        // 2. Fallback to the backup file
        string backupPath = _tunnelsFilePath + ".bak";
        if (File.Exists(backupPath))
        {
            try
            {
                string json = File.ReadAllText(backupPath);
                var tunnels = JsonConvert.DeserializeObject<List<Tunnel>>(json, settings);
                if (tunnels != null)
                {
                    return tunnels;
                }
            }
            catch (Exception ex)
            {
                Logger.DisplayAndLogError(ex, "Backup tunnels file is also corrupted.");
            }
        }

        return new List<Tunnel>();
    }

    public static void SaveTunnels(List<Tunnel> tunnels)
    {
        try
        {
            Logger.EnsureDirectoryExist(_tunnelsFilePath);
            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                Converters = { new StringEnumConverter(), new Vector3Converter() },
                Formatting = Formatting.Indented
            };
            string json = JsonConvert.SerializeObject(tunnels, settings);

            // USE THE SAFE WRITE METHOD HERE
            SafeWriteAllText(_tunnelsFilePath, json);
        }
        catch (Exception ex)
        {
            Logger.DisplayAndLogError(ex, "Failed to save tunnels.");
        }
    }

    /// <summary>
    /// Compares a specific settings section with the one in the JSON file. 
    /// If differences are found, updates the JSON file. If not, does nothing.
    /// </summary>
    public static void SaveSettingsSection<T>(T newSettings, Func<Configuration, T> getter, Action<Configuration, T> setter)
    {
        try
        {
            Configuration currentConfig = LoadConfiguration();
            T currentSettings = getter(currentConfig);

            var comparisonSettings = new JsonSerializerSettings
            {
                Formatting = Formatting.None,
                NullValueHandling = NullValueHandling.Ignore,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            };

            comparisonSettings.Converters.Add(new StringEnumConverter());
            comparisonSettings.Converters.Add(new Vector3Converter());

            string currentJson = JsonConvert.SerializeObject(currentSettings, comparisonSettings);
            string newJson = JsonConvert.SerializeObject(newSettings, comparisonSettings);

            if (currentJson != newJson)
            {
                setter(currentConfig, newSettings);
                SaveConfiguration(currentConfig); // This now uses the SafeWrite method under the hood

                Notification.PostTicker("~g~Settings saved successfully.", true);
            }
            else
            {
                Notification.PostTicker("~y~No changes detected. Settings not saved.", true);
            }
        }
        catch (Exception ex)
        {
            Logger.DisplayAndLogError(ex, "Failed to save settings section.");
        }
    }
}