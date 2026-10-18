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
using System.Reflection;

using static RealisticVehicleRadio.Helpers;

using GTA;

namespace RealisticVehicleRadio
{
    public sealed class Configuration
    {
        public RealisticVehicleRadioSettings RealisticVehicleRadio { get; set; } = new RealisticVehicleRadioSettings();
        public TunnelDebugVisualizerSettings TunnelDebugVisualizer { get; set; } = new TunnelDebugVisualizerSettings();
        public TunnelRecorderSettings TunnelRecorder { get; set; } = new TunnelRecorderSettings();

        public sealed class RealisticVehicleRadioSettings
        {
            public MainSettings Main { get; set; } = new MainSettings();
            public PerformanceSettings Performance { get; set; } = new PerformanceSettings(); // New in V1.3.0
            public LoudnessSettings Loudness { get; set; } = new LoudnessSettings();
            public TunnelDetectionSettings TunnelDetection { get; set; } = new TunnelDetectionSettings();
            public VehicleExclusions Exclusions { get; set; } = new VehicleExclusions();
            public ThunderstormSettings Thunderstorm { get; set; } = new ThunderstormSettings();
        }

        public sealed class MainSettings
        {
            private float _radioDisableDepth = 10.0f;
            private string _openMenuKey = "F3";

            /// <summary>Must be >= 0.</summary>
            public float RadioDisableDepth
            {
                get => _radioDisableDepth;
                set => _radioDisableDepth = Math.Max(0f, value);
            }

            public bool MuteRadioInTunnels { get; set; } = true;
            public bool EnableStaticRadioInTunnels { get; set; } = true;
            public bool DisableRadioInWater { get; set; } = true;
            public bool AutoCheckForUpdatesOnStartup { get; set; } = true;
            public string OpenMenuKey
            {
                get => _openMenuKey;
                set => _openMenuKey = string.IsNullOrWhiteSpace(value) ? "F3" : value; // Fallback to F3
            }
        }

        public sealed class PerformanceSettings
        {
            private const int _MIN_INTERVAL_MS = 100;

            private float _vehiclesSearchDistance = 50.0f;
            private int _maxProcessVehicles = 30;
            private int _logicUpdateIntervalMs = 100;
            private int _scanIntervalMs = 250;
            private int _maxCachedVehicles = 48;
            private float _maxProcessDistance = 60.0f;
            private float _removeDistance = 100.0f;

            /// <summary>Must be >= 0. Set to 0 to include all vehicles.</summary>
            public float VehiclesSearchDistance
            {
                get => _vehiclesSearchDistance;
                set => _vehiclesSearchDistance = Math.Max(0f, value);
            }

            /// <summary>Must be >= 0. Set to 0 to process all found vehicles.</summary>
            public int MaxProcessVehicles
            {
                get => _maxProcessVehicles;
                set => _maxProcessVehicles = Math.Max(0, value);
            }

            /// <summary>Minimum 100 ms. Values below are clamped to 100.</summary>
            public int LogicUpdateIntervalMs
            {
                get => _logicUpdateIntervalMs;
                set => _logicUpdateIntervalMs = Math.Max(_MIN_INTERVAL_MS, value);
            }

            /// <summary>Minimum 100 ms. Values below are clamped to 100.</summary>
            public int ScanIntervalMs
            {
                get => _scanIntervalMs;
                set => _scanIntervalMs = Math.Max(_MIN_INTERVAL_MS, value);
            }

            /// <summary>Must be >= 0. Set to 0 for unlimited cache.</summary>
            public int MaxCachedVehicles
            {
                get => _maxCachedVehicles;
                set => _maxCachedVehicles = Math.Max(0, value);
            }

            /// <summary>Must be >= 0. Set to 0 to process all cached vehicles.</summary>
            public float MaxProcessDistance
            {
                get => _maxProcessDistance;
                set => _maxProcessDistance = Math.Max(0f, value);
            }

            /// <summary>Must be >= 0. Set to 0 to disable the distance-removal feature</summary>
            public float RemoveDistance
            {
                get => _removeDistance;
                set => _removeDistance = Math.Max(0f, value);
            }
        }

        public sealed class LoudnessSettings
        {
            private int _randomLoudRadioChancePercentage = 50;
            private int _loudEchoVehicleCount = 0;
            public bool EnableRoofCheck { get; set; } = true;
            public bool EnableWindowCheck { get; set; } = true;
            public bool EnableDoorCheck { get; set; } = true;
            public bool EnableRandomChance { get; set; } = true;

            /// <summary>Must be between 0 and 100. Values outside this range are clamped to 100.</summary>
            public int RandomLoudRadioChancePercentage
            {
                get => _randomLoudRadioChancePercentage;
                set => _randomLoudRadioChancePercentage = MathFunctions.Clamp(value, 0, 100);
            }

            /// <summary>
            /// 0 disables the feature.
            /// Values above 0 spawn that many invisible loud echo vehicles
            /// around the player's last vehicle after exiting it, but only
            /// if the player vehicle was loud.
            /// </summary>
            public int LoudEchoVehicleCount
            {
                get => _loudEchoVehicleCount;
                set => _loudEchoVehicleCount = Math.Max(0, value);
            }
        }

        public sealed class TunnelDetectionSettings
        {
            private float _tunnelHorizontalTolerance = 0.2f;
            private float _tunnelVerticalTolerance = 0.5f;

            /// <summary>Must be >= 0. Set to 0 to remove horizontal tolerance.</summary>
            public float TunnelHorizontalTolerance
            {
                get => _tunnelHorizontalTolerance;
                set => _tunnelHorizontalTolerance = Math.Max(0f, value);
            }

            /// <summary>Must be >= 0. Set to 0 to remove vertical tolerance.</summary>
            public float TunnelVerticalTolerance
            {
                get => _tunnelVerticalTolerance;
                set => _tunnelVerticalTolerance = Math.Max(0f, value);
            }
        }

        public sealed class VehicleExclusions
        {
            private static readonly VehicleHash[] _defaultNoRadioVehicles =
            {
                VehicleHash.OpenWheel1,
                VehicleHash.OpenWheel2,
                VehicleHash.Formula,
                VehicleHash.Formula2
            };

            private VehicleHash[] _vehiclesWithNoRadio = (VehicleHash[])_defaultNoRadioVehicles.Clone();

            public bool DisableRadioForMotorcycles { get; set; } = true;
            public bool DisableRadioForPlanes { get; set; } = true;
            public bool DisableRadioForAircraft { get; set; } = true;
            public bool DisableRadioForQuadBikes { get; set; } = true;
            public bool DisableRadioForHelicopters { get; set; } = true;
            public bool DisableRadioForTrains { get; set; } = true;
            public bool DisableRadioForSubmarines { get; set; } = true;
            public bool DisableRadioForSubmarineCars { get; set; } = true;
            public bool DisableRadioForRegularQuadBikes { get; set; } = true;
            public bool DisableRadioForMilitaryVehicles { get; set; } = true;
            public VehicleHash[] VehiclesWithNoRadio
            {
                get => _vehiclesWithNoRadio;
                set => _vehiclesWithNoRadio = value != null
                    ? (VehicleHash[])value.Clone()
                    : (VehicleHash[])_defaultNoRadioVehicles.Clone();
            }
        }

        public sealed class TunnelDebugVisualizerSettings
        {
            private float _maxDrawDistance = 150.0f;

            private string _debugToggleKey = "F6";

            public string DebugToggleKey
            {
                get => _debugToggleKey;
                set => _debugToggleKey = string.IsNullOrWhiteSpace(value) ? "F6" : value; // Fallback to F6
            }

            /// <summary>Must be >= 10. Values below 10 are clamped to 10.</summary>
            public float MaxDrawDistance
            {
                get => _maxDrawDistance;
                set => _maxDrawDistance = Math.Max(10f, value);
            }
        }

        public sealed class TunnelRecorderSettings
        {
            private float _minDistanceMeters = 1.0f;
            private float _headingDeltaThresholdDeg = 5.0f;
            private float _maxHorizontalSearch = 30f;
            private float _maxVerticalSearch = 30f;
            private string _recordToggleKey = "F7";

            public string RecordToggleKey
            {
                get => _recordToggleKey;
                set => _recordToggleKey = string.IsNullOrWhiteSpace(value) ? "F7" : value; // Fallback to F7
            }

            // Sampling parameters
            /// <summary>Must be >= 0. Set to 0 to disable sampling by distance</summary>
            public float MinDistanceMeters // sample at least every ~1m
            {
                get => _minDistanceMeters;
                set => _minDistanceMeters = Math.Max(0f, value);
            }

            /// <summary>Must be >= 0. Set to 0 to disable sampling by delta heading</summary>
            public float HeadingDeltaThresholdDeg // if heading changes by >= this deg => force sample
            {
                get => _headingDeltaThresholdDeg;
                set => _headingDeltaThresholdDeg = Math.Max(0f, value);
            }

            public float MaxHorizontalSearch // how far to raycast left/right
            {
                get => _maxHorizontalSearch;
                set => _maxHorizontalSearch = Math.Max(0f, value);
            }

            public float MaxVerticalSearch // how far to raycast up/down
            {
                get => _maxVerticalSearch;
                set => _maxVerticalSearch = Math.Max(0f, value);
            }

            public float WallRayHeightOffset { get; set; } = 0.5f;      // cast slightly above ground to avoid ground hits
        }

        public sealed class ThunderstormSettings
        {
            private float _initialInterferenceDelayMin = 5.0f;
            private float _initialInterferenceDelayMax = 15.0f;
            private float _interferenceDurationMin = 0.5f;
            private float _interferenceDurationMax = 2.5f;
            private float _nextInterferenceDelayMin = 10.0f;
            private float _nextInterferenceDelayMax = 30.0f;
            private float _staticSignalMin = 0.4f;
            private float _staticSignalMax = 0.8f;
            private float _staticDriftSpeed = 0.6f;
            private float _staticHissIntensity = 0.15f;
            private int _staticPopChancePercentage = 8;
            private float _staticPopMaxIntensity = 0.35f;

            // =========================================================
            // MUTE BURSTS (Lightning Strikes / Heavy Interference)
            // =========================================================
            public bool EnableThunderstormInterference { get; set; } = true;

            // ================================================
            // BACKGROUND STATIC (Perlin Noise + XorShift)
            // ================================================
            public bool EnableThunderstormStatic { get; set; } = true;
            public bool EnableStaticPop { get; set; } = true;

            // Delay after THUNDER weather starts before the first interference event
            /// <summary>Must be >= 0.</summary>
            public float InitialInterferenceDelayMin
            {
                get => _initialInterferenceDelayMin;
                set => _initialInterferenceDelayMin = Math.Max(0f, value);
            }

            /// <summary>Must be >= 0.</summary>
            public float InitialInterferenceDelayMax
            {
                get => _initialInterferenceDelayMax;
                set => _initialInterferenceDelayMax = Math.Max(0f, value);
            }

            // Duration of the radio interference itself
            /// <summary>Must be >= 0.</summary>
            public float InterferenceDurationMin
            {
                get => _interferenceDurationMin;
                set => _interferenceDurationMin = Math.Max(0f, value);
            }

            /// <summary>Must be >= 0.</summary>
            public float InterferenceDurationMax
            {
                get => _interferenceDurationMax;
                set => _interferenceDurationMax = Math.Max(0f, value);
            }

            // Delay between one interference event ending and the next one starting
            /// <summary>Must be >= 0.</summary>
            public float NextInterferenceDelayMin
            {
                get => _nextInterferenceDelayMin;
                set => _nextInterferenceDelayMin = Math.Max(0f, value);
            }

            /// <summary>Must be >= 0.</summary>
            public float NextInterferenceDelayMax
            {
                get => _nextInterferenceDelayMax;
                set => _nextInterferenceDelayMax = Math.Max(0f, value);
            }

            // The range of the radio signal level during a thunderstorm (1.0 = perfect, 0.0 = dead air)
            // Lower values mean heavier static.
            /// <summary>Must be between 0.0 and 1.0.</summary>
            public float StaticSignalMin // Heaviest static baseline
            {
                get => _staticSignalMin;
                set => _staticSignalMin = MathFunctions.Clamp(value, 0f, 1f);
            }

            /// <summary>Must be between 0.0 and 1.0.</summary>
            public float StaticSignalMax // Lightest static baseline
            {
                get => _staticSignalMax;
                set => _staticSignalMax = MathFunctions.Clamp(value, 0f, 1f);
            }

            // Speed of the organic Perlin noise drift (higher = faster atmospheric changes)
            /// <summary>Must be >= 0.</summary>
            public float StaticDriftSpeed
            {
                get => _staticDriftSpeed;
                set => _staticDriftSpeed = Math.Max(0f, value);
            }

            // Intensity of the high-frequency Perlin hiss
            /// <summary>Must be >= 0.</summary>
            public float StaticHissIntensity
            {
                get => _staticHissIntensity;
                set => _staticHissIntensity = Math.Max(0f, value);
            }

            /// <summary>
            /// Probability of a static pop occurring per second, from 0 to 100 percent.
            /// </summary>
            public int StaticPopChancePercentage
            {
                get => _staticPopChancePercentage;
                set => _staticPopChancePercentage = MathFunctions.Clamp(value, 0, 100);
            }

            // Maximum signal deduction when a random pop occurs
            /// <summary>Must be between 0.0 and 1.0.</summary>
            public float StaticPopMaxIntensity
            {
                get => _staticPopMaxIntensity;
                set => _staticPopMaxIntensity = MathFunctions.Clamp(value, 0f, 1f);
            }

            /// <summary>
            /// Ensures Min values never exceed Max values after deserialization.
            /// Call this once after loading from JSON.
            /// </summary>
            public void NormalizeMinMaxPairs()
            {
                Normalize(ref _initialInterferenceDelayMin, ref _initialInterferenceDelayMax);
                Normalize(ref _interferenceDurationMin, ref _interferenceDurationMax);
                Normalize(ref _nextInterferenceDelayMin, ref _nextInterferenceDelayMax);
                Normalize(ref _staticSignalMin, ref _staticSignalMax);
            }

            private static void Normalize(ref float min, ref float max)
            {
                if (min > max)
                {
                    max = min;
                }
            }
        }

        public void ResetToDefaults()
        {
            CopyInto(new Configuration(), this);
        }

        private static void CopyInto(object source, object target)
        {
            if (source == null || target == null)
            {
                return;
            }

            PropertyInfo[] properties = source.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

            for (int i = 0; i < properties.Length; i++)
            {
                PropertyInfo property = properties[i];

                if (!property.CanRead)
                {
                    continue;
                }

                object sourceValue = property.GetValue(source, null);
                Type propertyType = property.PropertyType;

                if (sourceValue == null)
                {
                    if (property.CanWrite)
                    {
                        property.SetValue(target, null, null);
                    }

                    continue;
                }

                // Arrays: clone to avoid shared references
                if (propertyType.IsArray)
                {
                    if (property.CanWrite)
                    {
                        Array array = (Array)sourceValue;
                        property.SetValue(target, array.Clone(), null);
                    }

                    continue;
                }

                // Simple values: set directly
                if (propertyType.IsValueType || propertyType == typeof(string) || propertyType.IsEnum)
                {
                    if (property.CanWrite)
                    {
                        property.SetValue(target, sourceValue, null);
                    }

                    continue;
                }

                // Nested config classes: copy into existing object, do NOT replace reference
                object targetValue = property.GetValue(target, null);

                if (targetValue == null)
                {
                    if (property.CanWrite)
                    {
                        property.SetValue(target, sourceValue, null);
                    }
                }
                else
                {
                    CopyInto(sourceValue, targetValue);
                }
            }
        }
    }
}