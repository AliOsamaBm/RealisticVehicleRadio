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

using GTA;

public sealed class Configuration
{
    public RealisticVehicleRadioSettings RealisticVehicleRadio { get; set; } = new RealisticVehicleRadioSettings();
    public TunnelDebugVisualizerSettings TunnelDebugVisualizer { get; set; } = new TunnelDebugVisualizerSettings();
    public TunnelRecorderSettings TunnelRecorder { get; set; } = new TunnelRecorderSettings();

    public sealed class RealisticVehicleRadioSettings
    {
        public MainSettings Main { get; set; } = new MainSettings();
        public LoudnessSettings Loudness { get; set; } = new LoudnessSettings();
        public TunnelDetectionSettings TunnelDetection { get; set; } = new TunnelDetectionSettings();
        public VehicleExclusions Exclusions { get; set; } = new VehicleExclusions();
        public ThunderstormSettings Thunderstorm { get; set; } = new ThunderstormSettings();
    }

    public sealed class MainSettings
    {
        public float VehiclesSearchDistance { get; set; } = 50.0f;
        public int MaxProcessVehicles { get; set; } = 30;
        public float RadioDisableDepth { get; set; } = 10.0f;
        public bool DisableRadioInTunnels { get; set; } = true;
        public bool DisableRadioInWater { get; set; } = true;
        public bool AutoCheckForUpdatesOnStartup { get; set; } = true;
        public string OpenMenuKey { get; set; } = "F3";
    }

    public sealed class LoudnessSettings
    {
        public bool EnableRoofCheck { get; set; } = true;
        public bool EnableWindowCheck { get; set; } = true;
        public bool EnableDoorCheck { get; set; } = true;
        public bool EnableRandomChance { get; set; } = true;
        public int RandomLoudRadioChancePercentage { get; set; } = 50;
    }

    public sealed class TunnelDetectionSettings
    {
        public float TunnelHorizontalTolerance { get; set; } = 0.2f;
        public float TunnelVerticalTolerance { get; set; } = 0.5f;
    }

    public sealed class VehicleExclusions
    {
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
        public VehicleHash[] VehiclesWithNoRadio { get; set; } = new[]
        {
                VehicleHash.OpenWheel1,
                VehicleHash.OpenWheel2,
                VehicleHash.Formula,
                VehicleHash.Formula2
        };
    }

    public sealed class TunnelDebugVisualizerSettings
    {
        public string DebugToggleKey { get; set; } = "F6";
        public float MaxDrawDistance { get; set; } = 150.0f;
    }

    public sealed class TunnelRecorderSettings
    {
        public string RecordToggleKey { get; set; } = "F7";

        // Sampling parameters
        public float MinDistanceMeters { get; set; } = 1.0f;        // sample at least every ~1m
        public float HeadingDeltaThresholdDeg { get; set; } = 5.0f; // if heading changes by >= this deg => force sample
        public float MaxHorizontalSearch { get; set; } = 30f;       // how far to raycast left/right
        public float MaxVerticalSearch { get; set; } = 30f;         // how far to raycast up/down
        public float WallRayHeightOffset { get; set; } = 0.5f;      // cast slightly above ground to avoid ground hits
    }

    public sealed class ThunderstormSettings
    {
        public bool EnableThunderstormInterference { get; set; } = true;

        // Delay after THUNDER weather starts before the first interference event
        public float InitialInterferenceDelayMin { get; set; } = 5.0f;
        public float InitialInterferenceDelayMax { get; set; } = 15.0f;

        // Duration of the radio interference itself
        public float InterferenceDurationMin { get; set; } = 0.5f;
        public float InterferenceDurationMax { get; set; } = 2.5f;

        // Delay between one interference event ending and the next one starting
        public float NextInterferenceDelayMin { get; set; } = 10.0f;
        public float NextInterferenceDelayMax { get; set; } = 30.0f;
    }
}