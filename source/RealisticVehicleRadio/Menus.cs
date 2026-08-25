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
using System.Windows.Forms;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using LemonUI;
using LemonUI.Menus;
using static Helpers;

using GTA;
using GTA.UI;
using GTA.Native;

public sealed class Menus
{
    public static readonly ObjectPool UiMenuPool = new ObjectPool();
    public readonly NativeMenu MainMenu = GUI.CreateAndRegisterMenu(UiMenuPool, "Realistic Vehicle Radio", $"Main Menu {UpdateChecker.CurrentScriptVersion}");

    public static string FullOverlayText;
    public static string TypedOverlayText;
    public static int TypingIndex;
    public static int LastTypeTime;
    public static int Alpha = 0;
    private const int TYPE_SPEED = 25; // ms per character (lower = faster)

    public static bool WaitingForKeyAssignment = false;
    public static NativeMenu PendingMenu = null;
    public static NativeListItem<string> PendingListItem = null;
    public static PendingKeyBind PendingKey = PendingKeyBind.None;

    private NativeMenu _mainSettingsTreeMenu;
    private NativeMenu _realisticVehicleRadioSettingsMenu;
    private NativeMenu _mainSettingsMenu;
    public static NativeMenu TunnelDebugVisualizerSettingsMenu;
    private NativeMenu _tunnelRecorderSettingsMenu;
    private NativeMenu _loudnessSettingsMenu;
    private NativeMenu _tunnelDetectionSettingsMenu;
    private NativeMenu _exclusionsSettingsMenu;
    public static NativeMenu ManualRecordingMenu;
    private NativeMenu _thunderstormSettingsMenu;
    public static bool IsManualRecordingActive => TunnelRecorder.EnableManualRecording;

    public Menus(Configuration.MainSettings mainCfg, Configuration.LoudnessSettings loudnessCfg, Configuration.TunnelDetectionSettings tunnelCfg, Configuration.VehicleExclusions exclusionsCfg, Configuration.TunnelDebugVisualizerSettings debugCfg, Configuration.TunnelRecorderSettings recorderCfg, Configuration.ThunderstormSettings thunderCfg)
    {
        InitializeMainTreeSettingsMenu();
        InitializeRadioSettingsMenu();
        InitializeDebugSettingsMenu(debugCfg);
        InitializeRecorderSettingsMenu(recorderCfg);
        InitializeMainRadioSettingsMenu(mainCfg);
        InitializeLoudnessSettingsMenu(loudnessCfg);
        InitializeThunderstormSettingsMenu(thunderCfg);
        InitializeTunnelSettingsMenu(tunnelCfg);
        InitializeExclusionsSettingsMenu(exclusionsCfg);
        InitializeTunnelManagerMenu();
    }

    private void InitializeMainTreeSettingsMenu()
    {
        _mainSettingsTreeMenu = GUI.CreateAndRegisterMenu(UiMenuPool, "Settings", "Configure Global Mod Preferences");
        MainMenu.AddSubMenu(_mainSettingsTreeMenu, "").Title = "Settings";
    }

    private void InitializeRadioSettingsMenu()
    {
        _realisticVehicleRadioSettingsMenu = GUI.CreateAndRegisterMenu(UiMenuPool, "Radio Settings", "Adjust Core Radio Mechanics & Behaviors");
        _mainSettingsTreeMenu.AddSubMenu(_realisticVehicleRadioSettingsMenu, "").Title = "Radio Settings";
    }

    private void InitializeDebugSettingsMenu(Configuration.TunnelDebugVisualizerSettings debugCfg)
    {
        TunnelDebugVisualizerSettingsMenu = GUI.CreateAndRegisterMenu(UiMenuPool, "Debug Settings", "Toggle Tunnel Visualizer");

        float pendingMaxDistance = debugCfg.MaxDrawDistance;

        NativeListItem<string> toggleDebugKeyItem = GUI.CreateEnumListItem<Keys>("Toggle Debug Key", false, GetKeyIndex(debugCfg.DebugToggleKey), "Choose the key to toggle debug mode. ~y~Either scroll through the list or press enter then press the key you want to assign and press the save button.");
        BindKeyAssignment(TunnelDebugVisualizerSettingsMenu, toggleDebugKeyItem, PendingKeyBind.Debug, "Toggle Debug Key");

        NativeItem maxDrawDistance = new NativeItem($"Max Draw Distance ({pendingMaxDistance} m)", "Adjust how far, in meters, the script draws the tunnel visualizer. ~y~Enter a value of 0 or less to always draw regardless of the distance.");
        BindFloatInput(maxDrawDistance, val => pendingMaxDistance = val, "Max Draw Distance ({0} m)", "Enter a numeric value.");

        TunnelDebugVisualizerSettingsMenu.Add(maxDrawDistance);
        TunnelDebugVisualizerSettingsMenu.Add(toggleDebugKeyItem);
        AddSaveAndResetButtons(TunnelDebugVisualizerSettingsMenu,
    resetAction: () =>
    {
        pendingMaxDistance = 150f;
        maxDrawDistance.Title = $"Max Draw Distance ({pendingMaxDistance} m)";
        toggleDebugKeyItem.SelectedItem = "F6";
    },
    saveAction: () =>
    {
        debugCfg.MaxDrawDistance = pendingMaxDistance;
        debugCfg.DebugToggleKey = toggleDebugKeyItem.SelectedItem;
        FileOperations.SaveSettingsSection(debugCfg, cfg => cfg.TunnelDebugVisualizer, (cfg, settings) => cfg.TunnelDebugVisualizer = settings);
    }
);

        _mainSettingsTreeMenu.AddSubMenu(TunnelDebugVisualizerSettingsMenu, "").Title = "Debug Settings";
    }

    private void InitializeRecorderSettingsMenu(Configuration.TunnelRecorderSettings recorderCfg)
    {
        _tunnelRecorderSettingsMenu = GUI.CreateAndRegisterMenu(UiMenuPool, "Tunnel Recorder", "Record & Export Tunnel Coordinates");

        float pendingMinDistanceMeters = recorderCfg.MinDistanceMeters;
        float pendingHeadingDeltaThresholdDeg = recorderCfg.HeadingDeltaThresholdDeg;
        float pendingMaxHorizontalSearch = recorderCfg.MaxHorizontalSearch;
        float pendingMaxVerticalSearch = recorderCfg.MaxVerticalSearch;
        float pendingWallRayHeightOffset = recorderCfg.WallRayHeightOffset;

        NativeListItem<string> toggleRecordingKeyItem = GUI.CreateEnumListItem<Keys>("Recorder Debug Key", false, GetKeyIndex(recorderCfg.RecordToggleKey), "Choose the key to toggle recording mode. ~y~Either scroll through the list or press enter then press the key you want to assign and press the save button.");
        BindKeyAssignment(_tunnelRecorderSettingsMenu, toggleRecordingKeyItem, PendingKeyBind.Record, "Recorder Debug Key");

        NativeItem minDistanceMeters = new NativeItem($"Min Distance ({pendingMinDistanceMeters} m)", "Minimum distance the vehicle must travel before recording a new tunnel point. ~y~Higher values = fewer points (less detail).");
        NativeItem headingDeltaThresholdDeg = new NativeItem($"Heading Delta Threshold ({pendingHeadingDeltaThresholdDeg} °)", "Minimum change in vehicle direction (in degrees) to force a new tunnel point. ~y~Ensures sharp turns are captured accurately.");
        NativeItem maxHorizontalSearch = new NativeItem($"Max Horizontal Search ({pendingMaxHorizontalSearch} m)", "Maximum distance to search left and right for tunnel walls. ~y~Increase for very wide tunnels, decrease if it hits outside objects.");
        NativeItem maxVerticalSearch = new NativeItem($"Max Vertical Search ({pendingMaxVerticalSearch} m)", "Maximum distance to search up and down for the tunnel ceiling and floor. ~y~Increase if the tunnel is exceptionally tall.");
        NativeItem wallRayHeightOffset = new NativeItem($"Wall Ray Height Offset ({pendingWallRayHeightOffset} m)", "Vertical offset from the vehicle where the wall-detecting rays start. ~y~Adjust if the rays are hitting the ground or the vehicle itself.");

        NativeItem nextTunnelNameItem = new NativeItem($"Next Tunnel Name: {TunnelRecorder.PendingTunnelName}", "Set a custom name for the next tunnel you record. ~y~Click to type a name.");
        nextTunnelNameItem.Activated += (s, e) =>
        {
            string userInput = GUI.PromptUserForInput(false);

            TunnelRecorder.PendingTunnelName = userInput;
            nextTunnelNameItem.Title = $"Next Tunnel Name: {userInput}";
        };

        BindFloatInput(minDistanceMeters, val => pendingMinDistanceMeters = val, "Min Distance ({0} m)", "Enter a numeric value.");
        BindFloatInput(headingDeltaThresholdDeg, val => pendingHeadingDeltaThresholdDeg = val, "Heading Delta Threshold ({0} °)", "Enter a numeric value.");
        BindFloatInput(maxHorizontalSearch, val => pendingMaxHorizontalSearch = val, "Max Horizontal Search ({0} m)", "Enter a numeric value.");
        BindFloatInput(maxVerticalSearch, val => pendingMaxVerticalSearch = val, "Max Vertical Search ({0} m)", "Enter a numeric value.");
        BindFloatInput(wallRayHeightOffset, val => pendingWallRayHeightOffset = val, "Wall Ray Height Offset ({0} m)", "Enter a numeric value.");

        _tunnelRecorderSettingsMenu.Add(nextTunnelNameItem);
        _tunnelRecorderSettingsMenu.Add(minDistanceMeters);
        _tunnelRecorderSettingsMenu.Add(headingDeltaThresholdDeg);
        _tunnelRecorderSettingsMenu.Add(maxHorizontalSearch);
        _tunnelRecorderSettingsMenu.Add(maxVerticalSearch);
        _tunnelRecorderSettingsMenu.Add(wallRayHeightOffset);
        _tunnelRecorderSettingsMenu.Add(toggleRecordingKeyItem);

        ManualRecordingMenu = GUI.CreateAndRegisterMenu(UiMenuPool, "Manual Recording", "Manually add, remove, and save tunnel points");

        NativeCheckboxItem enableManualRecording = GUI.CreateCheckboxItem(
            "Enable Manual Recording",
            TunnelRecorder.EnableManualRecording,
            "Toggles manual recording mode. When enabled, preview lines are drawn around your vehicle and the automatic recording key is disabled."
        );

        // Sync the checkbox state through the safe manual-mode switch.
        enableManualRecording.CheckboxChanged += (s, e) =>
        {
            if (!TunnelRecorder.TrySetManualRecording(enableManualRecording.Checked))
            {
                // Revert checkbox if manual mode could not be enabled.
                enableManualRecording.Checked = TunnelRecorder.EnableManualRecording;
            }
        };

        // Ensure the checkbox reflects the correct state every time the menu is opened.
        ManualRecordingMenu.Opening += (s, e) =>
        {
            enableManualRecording.Checked = TunnelRecorder.EnableManualRecording;
        };

        NativeItem addPointBtn = new NativeItem(
            "Add Point",
            "Adds a tunnel point to the manual session at your current vehicle's location based on the raycast settings."
        );

        addPointBtn.Activated += (s, e) =>
        {
            Ped player = Game.Player.Character;

            if (ValidFunctions.IsValidPed(player) && ValidFunctions.IsValidVehicle(player.CurrentVehicle))
            {
                TunnelRecorder.AddManualPoint(player.CurrentVehicle, recorderCfg);
            }
            else
            {
                Notification.PostTicker("~r~You must be in a vehicle to add a point.", true);
            }
        };

        NativeItem deletePrevBtn = new NativeItem(
            "Delete Previous Point",
            "Removes the last added tunnel point from the current manual session."
        );

        deletePrevBtn.Activated += (s, e) =>
        {
            TunnelRecorder.DeletePreviousPoint();
        };

        NativeItem clearManualPointsBtn = new NativeItem(
            "~r~Clear Manual Points",
            "Discards all unsaved manual tunnel points."
        );

        clearManualPointsBtn.Activated += (s, e) =>
        {
            TunnelRecorder.ClearManualPoints();
        };

        NativeItem saveChangesBtn = new NativeItem(
            "~g~Save Changes",
            "Saves all currently added manual points as a new tunnel and clears the manual session. Requires at least 2 points."
        );

        saveChangesBtn.Activated += (s, e) =>
        {
            TunnelRecorder.SaveManualChanges();
        };

        ManualRecordingMenu.Add(enableManualRecording);
        ManualRecordingMenu.Add(addPointBtn);
        ManualRecordingMenu.Add(deletePrevBtn);
        ManualRecordingMenu.Add(clearManualPointsBtn);
        ManualRecordingMenu.Add(saveChangesBtn);

        _tunnelRecorderSettingsMenu.AddSubMenu(ManualRecordingMenu).Title = "~y~Manual Recording";

        AddSaveAndResetButtons(_tunnelRecorderSettingsMenu,
    resetAction: () =>
    {
        pendingMinDistanceMeters = 1.0f; minDistanceMeters.Title = $"Min Distance ({pendingMinDistanceMeters} m)";
        pendingHeadingDeltaThresholdDeg = 5.0f; headingDeltaThresholdDeg.Title = $"Heading Delta Threshold ({pendingHeadingDeltaThresholdDeg} °)";
        pendingMaxHorizontalSearch = 30.0f; maxHorizontalSearch.Title = $"Max Horizontal Search ({pendingMaxHorizontalSearch} m)";
        pendingMaxVerticalSearch = 30.0f; maxVerticalSearch.Title = $"Max Vertical Search ({pendingMaxVerticalSearch} m)";
        pendingWallRayHeightOffset = 0.5f; wallRayHeightOffset.Title = $"Wall Ray Height Offset ({pendingWallRayHeightOffset} m)";
        toggleRecordingKeyItem.SelectedItem = "F7";
        TunnelRecorder.PendingTunnelName = "New Tunnel";
        nextTunnelNameItem.Title = $"Next Tunnel Name: {TunnelRecorder.PendingTunnelName}";
    },
    saveAction: () =>
    {
        recorderCfg.MinDistanceMeters = pendingMinDistanceMeters;
        recorderCfg.HeadingDeltaThresholdDeg = pendingHeadingDeltaThresholdDeg;
        recorderCfg.MaxHorizontalSearch = pendingMaxHorizontalSearch;
        recorderCfg.MaxVerticalSearch = pendingMaxVerticalSearch;
        recorderCfg.WallRayHeightOffset = pendingWallRayHeightOffset;
        recorderCfg.RecordToggleKey = toggleRecordingKeyItem.SelectedItem;
        FileOperations.SaveSettingsSection(recorderCfg, cfg => cfg.TunnelRecorder, (cfg, settings) => cfg.TunnelRecorder = settings);
    }
);

        _mainSettingsTreeMenu.AddSubMenu(_tunnelRecorderSettingsMenu, "").Title = "Tunnel Recorder";
    }

    private void InitializeMainRadioSettingsMenu(Configuration.MainSettings mainCfg)
    {
        _mainSettingsMenu = GUI.CreateAndRegisterMenu(UiMenuPool, "Main Settings", "General Configuration Options");

        float pendingVehiclesSearchDistance = mainCfg.VehiclesSearchDistance;
        float pendingRadioDisableDepth = mainCfg.RadioDisableDepth;
        int pendingMaxProcessVehicles = mainCfg.MaxProcessVehicles;

        NativeListItem<string> openMenuKeyItem = GUI.CreateEnumListItem<Keys>("Open Menu Key", false, GetKeyIndex(mainCfg.OpenMenuKey), "Choose the key to open main menu. ~y~Either scroll through the list or press enter then press the key you want to assign and press the save button.");
        BindKeyAssignment(_mainSettingsMenu, openMenuKeyItem, PendingKeyBind.OpenMenu, "Open Menu Key");

        NativeItem vehiclesSearchDistance = new NativeItem($"Vehicles Search Distance ({pendingVehiclesSearchDistance} m)", "Adjust how far, in meters, the script processes vehicles. ~y~Enter a value of 0 or less to process all vehicles regardless of the distance. Press the save button to apply the changes.");
        BindFloatInput(vehiclesSearchDistance, val => pendingVehiclesSearchDistance = val, "Vehicles Search Distance ({0} m)", "Enter a numeric value.");

        NativeItem maxProcessVehicles = new NativeItem($"Max Process Vehicles ({pendingMaxProcessVehicles})", "Limits the maximum number of vehicles processed per frame. ~y~Set to 0 or lower to ignore this limit and process all vehicles within the search radius. Useful for performance in heavy traffic.");
        BindIntInput(maxProcessVehicles, val => pendingMaxProcessVehicles = val, "Max Process Vehicles ({0})", "Enter a numeric value.");

        NativeItem radioDisableDepthDistance = new NativeItem($"Radio Disable Depth ({pendingRadioDisableDepth} m)", "Determines how deep a vehicle must be underwater before its radio stops working. Higher values allow the radio to continue playing longer while submerged. ~y~Press the save button to apply the changes.");
        BindFloatInput(radioDisableDepthDistance, val => pendingRadioDisableDepth = val, "Radio Disable Depth ({0} m)", "Enter a numeric value.");

        NativeCheckboxItem disableRadioInTunnels = GUI.CreateCheckboxItem("Disable Radio In Tunnels", mainCfg.DisableRadioInTunnels);

        NativeCheckboxItem disableRadioInWater = GUI.CreateCheckboxItem("Disable Radio In Water", mainCfg.DisableRadioInWater);

        //NativeCheckboxItem autoCheckUpdatesItem = GUI.CreateCheckboxItem("Auto-check for updates on startup", mainCfg.AutoCheckForUpdatesOnStartup, "Automatically checks for script updates when the game starts. ~y~Press the save button to apply changes.");

        //NativeItem checkUpdatesBtn = new NativeItem("~b~Check for Updates Now");
        //checkUpdatesBtn.Activated += (s, i) =>
        //{
        //    Task.Run(async () => await UpdateChecker.CheckForUpdatesAsync());
        //};

        _mainSettingsMenu.Add(vehiclesSearchDistance);
        _mainSettingsMenu.Add(maxProcessVehicles);
        _mainSettingsMenu.Add(radioDisableDepthDistance);
        _mainSettingsMenu.Add(disableRadioInTunnels);
        _mainSettingsMenu.Add(disableRadioInWater);
        //_mainSettingsMenu.Add(autoCheckUpdatesItem);
        _mainSettingsMenu.Add(openMenuKeyItem);
        //_mainSettingsMenu.Add(checkUpdatesBtn);
        AddSaveAndResetButtons(_mainSettingsMenu,
    resetAction: () =>
    {
        pendingVehiclesSearchDistance = 50f;
        pendingMaxProcessVehicles = 30;
        pendingRadioDisableDepth = 10f;
        vehiclesSearchDistance.Title = $"Vehicles Search Distance ({pendingVehiclesSearchDistance} m)";
        maxProcessVehicles.Title = $"Max Process Vehicles ({pendingMaxProcessVehicles})";
        radioDisableDepthDistance.Title = $"Radio Disable Depth ({pendingRadioDisableDepth} m)";
        disableRadioInTunnels.Checked = true;
        disableRadioInWater.Checked = true;
        //autoCheckUpdatesItem.Checked = true;
        openMenuKeyItem.SelectedItem = "F3";
    },
    saveAction: () =>
    {
        mainCfg.VehiclesSearchDistance = pendingVehiclesSearchDistance;
        mainCfg.MaxProcessVehicles = pendingMaxProcessVehicles;
        mainCfg.RadioDisableDepth = pendingRadioDisableDepth;
        mainCfg.DisableRadioInTunnels = disableRadioInTunnels.Checked;
        mainCfg.DisableRadioInWater = disableRadioInWater.Checked;
        //mainCfg.AutoCheckForUpdatesOnStartup = autoCheckUpdatesItem.Checked;
        mainCfg.OpenMenuKey = openMenuKeyItem.SelectedItem;
        FileOperations.SaveSettingsSection(mainCfg, cfg => cfg.RealisticVehicleRadio.Main, (cfg, settings) => cfg.RealisticVehicleRadio.Main = settings);
    }
);

        _realisticVehicleRadioSettingsMenu.AddSubMenu(_mainSettingsMenu, "").Title = "Main Settings";
    }

    private void InitializeLoudnessSettingsMenu(Configuration.LoudnessSettings loudnessCfg)
    {
        _loudnessSettingsMenu = GUI.CreateAndRegisterMenu(UiMenuPool, "Radio Loudness", "Manage Loud Radio Settings");

        int pendingRandomLoudRadioChance = loudnessCfg.RandomLoudRadioChancePercentage;

        NativeItem randomLoudRadioChancePercentage = new NativeItem($"Random Loud Radio Chance ({pendingRandomLoudRadioChance} %)", "Adjust the chances of getting a loud radio in a vehicle after all other checks.");
        BindIntInput(randomLoudRadioChancePercentage, val => pendingRandomLoudRadioChance = val, "Random Loud Radio Chance ({0} %)", "Enter a numeric value between 0 and 100.", v => v >= 0 && v <= 100);

        NativeCheckboxItem enableRoofCheck = GUI.CreateCheckboxItem(
    "Enable Roof Check",
    loudnessCfg.EnableRoofCheck,
    "Automatically sets radio to loud when the convertible top or sunroof is open or opening."
);

        NativeCheckboxItem enableWindowCheck = GUI.CreateCheckboxItem(
            "Enable Window Check",
            loudnessCfg.EnableWindowCheck,
            "Automatically sets radio to loud if any window in the vehicle is broken or shattered."
        );

        NativeCheckboxItem enableDoorCheck = GUI.CreateCheckboxItem(
            "Enable Door Check",
            loudnessCfg.EnableDoorCheck,
            "Automatically sets radio to loud if any door is open, ajar, or broken off."
        );

        NativeCheckboxItem enableRandomChance = GUI.CreateCheckboxItem(
            "Enable Random Chance",
            loudnessCfg.EnableRandomChance,
            "Gives fully closed vehicles a random chance to have a loud radio. ~y~Vehicles with open roofs, broken windows, or open doors are guaranteed to be loud and skip this roll."
        );

        _loudnessSettingsMenu.Add(enableRoofCheck);
        _loudnessSettingsMenu.Add(enableWindowCheck);
        _loudnessSettingsMenu.Add(enableDoorCheck);
        _loudnessSettingsMenu.Add(enableRandomChance);
        _loudnessSettingsMenu.Add(randomLoudRadioChancePercentage);
        AddSaveAndResetButtons(_loudnessSettingsMenu,
    resetAction: () =>
    {
        pendingRandomLoudRadioChance = 50;
        randomLoudRadioChancePercentage.Title = $"Random Loud Radio Chance ({pendingRandomLoudRadioChance} %)";
        enableRoofCheck.Checked = true;
        enableWindowCheck.Checked = true;
        enableDoorCheck.Checked = true;
        enableRandomChance.Checked = true;
    },
    saveAction: () =>
    {
        loudnessCfg.RandomLoudRadioChancePercentage = pendingRandomLoudRadioChance;
        loudnessCfg.EnableRoofCheck = enableRoofCheck.Checked;
        loudnessCfg.EnableWindowCheck = enableWindowCheck.Checked;
        loudnessCfg.EnableDoorCheck = enableDoorCheck.Checked;
        loudnessCfg.EnableRandomChance = enableRandomChance.Checked;
        FileOperations.SaveSettingsSection(loudnessCfg, cfg => cfg.RealisticVehicleRadio.Loudness, (cfg, settings) => cfg.RealisticVehicleRadio.Loudness = settings);
    }
);

        _realisticVehicleRadioSettingsMenu.AddSubMenu(_loudnessSettingsMenu, "").Title = "Radio Loudness";
    }

    private void InitializeTunnelSettingsMenu(Configuration.TunnelDetectionSettings tunnelCfg)
    {
        _tunnelDetectionSettingsMenu = GUI.CreateAndRegisterMenu(UiMenuPool, "Tunnel Detection", "Tweak Detection Radius & Muting Rules");

        float pendingTunnelHorizontalTolerance = tunnelCfg.TunnelHorizontalTolerance;
        float pendingTunnelVerticalTolerance = tunnelCfg.TunnelVerticalTolerance;

        NativeItem tunnelHorizontalTolerance = new NativeItem($"Horizontal Tolerance ({pendingTunnelHorizontalTolerance} m)", "Adds extra horizontal leeway to the tunnel walls. Prevents the radio from cutting out if your recording is slightly off-center.");
        NativeItem tunnelVerticalTolerance = new NativeItem($"Vertical Tolerance ({pendingTunnelVerticalTolerance} m)", "Adds extra vertical space above the ceiling and below the floor. Useful for bumpy roads, ramps, or sloped tunnel entrances.");

        BindFloatInput(tunnelHorizontalTolerance, val => pendingTunnelHorizontalTolerance = val, "Horizontal Tolerance ({0} m)", "Enter a positive number.", v => v >= 0);
        BindFloatInput(tunnelVerticalTolerance, val => pendingTunnelVerticalTolerance = val, "Vertical Tolerance ({0} m)", "Enter a positive number.", v => v >= 0);

        _tunnelDetectionSettingsMenu.Add(tunnelHorizontalTolerance);
        _tunnelDetectionSettingsMenu.Add(tunnelVerticalTolerance);

        AddSaveAndResetButtons(_tunnelDetectionSettingsMenu,
    resetAction: () =>
    {
        pendingTunnelHorizontalTolerance = 0.2f; tunnelHorizontalTolerance.Title = $"Horizontal Tolerance ({pendingTunnelHorizontalTolerance} m)";
        pendingTunnelVerticalTolerance = 0.5f; tunnelVerticalTolerance.Title = $"Vertical Tolerance ({pendingTunnelVerticalTolerance} m)";
    },
saveAction: () =>
{
    tunnelCfg.TunnelHorizontalTolerance = pendingTunnelHorizontalTolerance;
    tunnelCfg.TunnelVerticalTolerance = pendingTunnelVerticalTolerance;

    FileOperations.SaveSettingsSection(
        tunnelCfg,
        cfg => cfg.RealisticVehicleRadio.TunnelDetection,
        (cfg, settings) => cfg.RealisticVehicleRadio.TunnelDetection = settings
    );

    TunnelSpatialIndex.MarkDirty();
}
);

        _realisticVehicleRadioSettingsMenu.AddSubMenu(_tunnelDetectionSettingsMenu, "").Title = "Tunnel Detection";
    }

    private void InitializeExclusionsSettingsMenu(Configuration.VehicleExclusions exclusionsCfg)
    {
        _exclusionsSettingsMenu = GUI.CreateAndRegisterMenu(UiMenuPool, "Excluded Vehicles", "Vehicles Exempt from Radio Logic");

        NativeCheckboxItem disableRadioForMotorcycles = GUI.CreateCheckboxItem("Disable Radio For Motorcycles", exclusionsCfg.DisableRadioForMotorcycles);
        NativeCheckboxItem disableRadioForPlanes = GUI.CreateCheckboxItem("Disable Radio For Planes", exclusionsCfg.DisableRadioForPlanes);
        NativeCheckboxItem disableRadioForAircraft = GUI.CreateCheckboxItem("Disable Radio For Aircraft", exclusionsCfg.DisableRadioForAircraft);
        NativeCheckboxItem disableRadioForQuadBikes = GUI.CreateCheckboxItem("Disable Radio For Quad Bikes", exclusionsCfg.DisableRadioForQuadBikes);
        NativeCheckboxItem disableRadioForHelicopters = GUI.CreateCheckboxItem("Disable Radio For Helicopters", exclusionsCfg.DisableRadioForHelicopters);
        NativeCheckboxItem disableRadioForTrains = GUI.CreateCheckboxItem("Disable Radio For Trains", exclusionsCfg.DisableRadioForTrains);
        NativeCheckboxItem disableRadioForSubmarines = GUI.CreateCheckboxItem("Disable Radio For Submarines", exclusionsCfg.DisableRadioForSubmarines);
        NativeCheckboxItem disableRadioForSubmarineCars = GUI.CreateCheckboxItem("Disable Radio For Submarine Cars", exclusionsCfg.DisableRadioForSubmarineCars);
        NativeCheckboxItem disableRadioForRegularQuadBikes = GUI.CreateCheckboxItem("Disable Radio For Regular Quad Bikes", exclusionsCfg.DisableRadioForRegularQuadBikes);
        NativeCheckboxItem disableRadioForMilitaryVehicles = GUI.CreateCheckboxItem("Disable Radio For Military Vehicles", exclusionsCfg.DisableRadioForMilitaryVehicles);

        NativeMenu excludedVehiclesHashes = GUI.CreateAndRegisterMenu(UiMenuPool, "Excluded Vehicles", "Add or remove specific vehicle models");
        List<NativeItem> vehicleItems = new List<NativeItem>();

        NativeItem addCurrentVehicleBtn = new NativeItem("~g~Add Current Vehicle", "Adds the vehicle you are currently sitting in to the list to disable radio for it. ~y~Press the save changes button in the previous menu to save changes.");
        addCurrentVehicleBtn.Activated += (s, e) =>
        {
            Ped player = Game.Player.Character;
            if (ValidFunctions.IsValidPed(player) && ValidFunctions.IsValidVehicle(player.CurrentVehicle))
            {
                VehicleHash hash = (VehicleHash)player.CurrentVehicle.Model.Hash;
                List<VehicleHash> list = new List<VehicleHash>(exclusionsCfg.VehiclesWithNoRadio);

                if (!list.Contains(hash))
                {
                    list.Add(hash);
                    exclusionsCfg.VehiclesWithNoRadio = list.ToArray();

                    NativeItem newItem = new NativeItem(hash.ToString(), "Click to remove from exclusion list.");
                    newItem.Activated += (s2, e2) =>
                    {
                        List<VehicleHash> currentList = new List<VehicleHash>(exclusionsCfg.VehiclesWithNoRadio);
                        currentList.Remove(hash);
                        exclusionsCfg.VehiclesWithNoRadio = currentList.ToArray();

                        excludedVehiclesHashes.Remove(newItem);
                        vehicleItems.Remove(newItem);
                        Notification.PostTicker($"Removed {hash}.", true);
                    };

                    excludedVehiclesHashes.Add(newItem);
                    vehicleItems.Add(newItem);
                    Notification.PostTicker($"Added {hash} to exclusions.", true);
                }
                else
                {
                    Notification.PostTicker("Vehicle is already in the list.", true);
                }
            }
            else
            {
                Notification.PostTicker("You must be in a vehicle to add it.", true);
            }
        };

        void BuildVehicleList()
        {
            for (int i = 0; i < vehicleItems.Count; i++) excludedVehiclesHashes.Remove(vehicleItems[i]);
            vehicleItems.Clear();

            for (int i = 0; i < exclusionsCfg.VehiclesWithNoRadio.Length; i++)
            {
                VehicleHash hash = exclusionsCfg.VehiclesWithNoRadio[i];
                NativeItem item = new NativeItem(hash.ToString(), "Click to remove from exclusion list.");
                item.Activated += (s, e) =>
                {
                    List<VehicleHash> list = new List<VehicleHash>(exclusionsCfg.VehiclesWithNoRadio);
                    list.Remove(hash);
                    exclusionsCfg.VehiclesWithNoRadio = list.ToArray();

                    excludedVehiclesHashes.Remove(item);
                    vehicleItems.Remove(item);
                    Notification.PostTicker($"Removed {hash}.", true);
                };

                excludedVehiclesHashes.Add(item);
                vehicleItems.Add(item);
            }
        }

        excludedVehiclesHashes.Add(addCurrentVehicleBtn);
        BuildVehicleList();

        _exclusionsSettingsMenu.Add(disableRadioForMotorcycles);
        _exclusionsSettingsMenu.Add(disableRadioForPlanes);
        _exclusionsSettingsMenu.Add(disableRadioForAircraft);
        _exclusionsSettingsMenu.Add(disableRadioForQuadBikes);
        _exclusionsSettingsMenu.Add(disableRadioForHelicopters);
        _exclusionsSettingsMenu.Add(disableRadioForTrains);
        _exclusionsSettingsMenu.Add(disableRadioForSubmarines);
        _exclusionsSettingsMenu.Add(disableRadioForSubmarineCars);
        _exclusionsSettingsMenu.Add(disableRadioForRegularQuadBikes);
        _exclusionsSettingsMenu.Add(disableRadioForMilitaryVehicles);

        _exclusionsSettingsMenu.AddSubMenu(excludedVehiclesHashes).Title = "Manage Excluded Vehicles";

        AddSaveAndResetButtons(_exclusionsSettingsMenu,
            resetAction: () =>
            {
                disableRadioForMotorcycles.Checked = true;
                disableRadioForPlanes.Checked = true;
                disableRadioForAircraft.Checked = true;
                disableRadioForQuadBikes.Checked = true;
                disableRadioForHelicopters.Checked = true;
                disableRadioForTrains.Checked = true;
                disableRadioForSubmarines.Checked = true;
                disableRadioForSubmarineCars.Checked = true;
                disableRadioForRegularQuadBikes.Checked = true;
                disableRadioForMilitaryVehicles.Checked = true;

                exclusionsCfg.VehiclesWithNoRadio = new[] { VehicleHash.OpenWheel1, VehicleHash.OpenWheel2, VehicleHash.Formula, VehicleHash.Formula2 };
                BuildVehicleList();
                Notification.PostTicker("Defaults restored. Press Save to apply.", true);
            },
            saveAction: () =>
            {
                exclusionsCfg.DisableRadioForMotorcycles = disableRadioForMotorcycles.Checked;
                exclusionsCfg.DisableRadioForPlanes = disableRadioForPlanes.Checked;
                exclusionsCfg.DisableRadioForAircraft = disableRadioForAircraft.Checked;
                exclusionsCfg.DisableRadioForQuadBikes = disableRadioForQuadBikes.Checked;
                exclusionsCfg.DisableRadioForHelicopters = disableRadioForHelicopters.Checked;
                exclusionsCfg.DisableRadioForTrains = disableRadioForTrains.Checked;
                exclusionsCfg.DisableRadioForSubmarines = disableRadioForSubmarines.Checked;
                exclusionsCfg.DisableRadioForSubmarineCars = disableRadioForSubmarineCars.Checked;
                exclusionsCfg.DisableRadioForRegularQuadBikes = disableRadioForRegularQuadBikes.Checked;
                exclusionsCfg.DisableRadioForMilitaryVehicles = disableRadioForMilitaryVehicles.Checked;

                FileOperations.SaveSettingsSection(exclusionsCfg, cfg => cfg.RealisticVehicleRadio.Exclusions, (cfg, settings) => cfg.RealisticVehicleRadio.Exclusions = settings);
            }
        );

        _realisticVehicleRadioSettingsMenu.AddSubMenu(_exclusionsSettingsMenu, "").Title = "Excluded Vehicles";
    }

    private void InitializeTunnelManagerMenu()
    {
        NativeMenu manageMenu = GUI.CreateAndRegisterMenu(UiMenuPool, "Manage Tunnels", "Delete Recorded Tunnels");

        // Call this whenever the menu is opened to refresh the list
        manageMenu.Shown += (sender, args) =>
        {
            manageMenu.Clear();
            for (int i = 0; i < TunnelManager.Tunnels.Count; i++)
            {
                Tunnel tunnel = TunnelManager.Tunnels[i];

                // Show the custom name in the title, and ID/Points in the description
                NativeItem item = new NativeItem(
                    $"Delete: {tunnel.Name}",
                    $"ID: #{tunnel.Id} | Points: {tunnel.Points.Count}"
                );

                item.Activated += (s, e) =>
                {
                    if (item.Title != "~r~Click Again to Confirm")
                    {
                        item.Title = "~r~Click Again to Confirm";
                    }
                    else
                    {
                        TunnelManager.DeleteTunnel(tunnel.Id);
                        manageMenu.Visible = false;
                    }
                };

                manageMenu.Add(item);
            }
        };

        MainMenu.AddSubMenu(manageMenu, "").Title = "Manage Tunnels";
    }

    private void InitializeThunderstormSettingsMenu(Configuration.ThunderstormSettings thunderCfg)
    {
        _thunderstormSettingsMenu = GUI.CreateAndRegisterMenu(
            UiMenuPool,
            "Thunderstorm Interference",
            "Brief radio interference during thunderstorms"
        );

        float pendingInitialMin = thunderCfg.InitialInterferenceDelayMin;
        float pendingInitialMax = thunderCfg.InitialInterferenceDelayMax;

        float pendingDurationMin = thunderCfg.InterferenceDurationMin;
        float pendingDurationMax = thunderCfg.InterferenceDurationMax;

        float pendingNextMin = thunderCfg.NextInterferenceDelayMin;
        float pendingNextMax = thunderCfg.NextInterferenceDelayMax;

        NativeCheckboxItem enableThunderInterference = GUI.CreateCheckboxItem(
            "Enable Thunderstorm Interference",
            thunderCfg.EnableThunderstormInterference,
            "When enabled, vehicle radios briefly cut out during THUNDER weather."
        );

        NativeItem initialDelayMin = new NativeItem(
            $"Initial Delay Min ({pendingInitialMin} s)",
            "Minimum delay after a thunderstorm starts before the first interference event."
        );

        NativeItem initialDelayMax = new NativeItem(
            $"Initial Delay Max ({pendingInitialMax} s)",
            "Maximum delay after a thunderstorm starts before the first interference event."
        );

        NativeItem durationMin = new NativeItem(
            $"Interference Duration Min ({pendingDurationMin} s)",
            "Minimum duration of each radio interference event."
        );

        NativeItem durationMax = new NativeItem(
            $"Interference Duration Max ({pendingDurationMax} s)",
            "Maximum duration of each radio interference event."
        );

        NativeItem nextDelayMin = new NativeItem(
            $"Next Delay Min ({pendingNextMin} s)",
            "Minimum delay between interference events."
        );

        NativeItem nextDelayMax = new NativeItem(
            $"Next Delay Max ({pendingNextMax} s)",
            "Maximum delay between interference events."
        );

        BindFloatInput(initialDelayMin, val => pendingInitialMin = val, "Initial Delay Min ({0} s)", "Enter a value of 0 or more.", v => v >= 0f);
        BindFloatInput(initialDelayMax, val => pendingInitialMax = val, "Initial Delay Max ({0} s)", "Enter a value of 0 or more.", v => v >= 0f);
        BindFloatInput(durationMin, val => pendingDurationMin = val, "Interference Duration Min ({0} s)", "Enter a value of 0 or more.", v => v >= 0f);
        BindFloatInput(durationMax, val => pendingDurationMax = val, "Interference Duration Max ({0} s)", "Enter a value of 0 or more.", v => v >= 0f);
        BindFloatInput(nextDelayMin, val => pendingNextMin = val, "Next Delay Min ({0} s)", "Enter a value of 0 or more.", v => v >= 0f);
        BindFloatInput(nextDelayMax, val => pendingNextMax = val, "Next Delay Max ({0} s)", "Enter a value of 0 or more.", v => v >= 0f);

        _thunderstormSettingsMenu.Add(enableThunderInterference);
        _thunderstormSettingsMenu.Add(initialDelayMin);
        _thunderstormSettingsMenu.Add(initialDelayMax);
        _thunderstormSettingsMenu.Add(durationMin);
        _thunderstormSettingsMenu.Add(durationMax);
        _thunderstormSettingsMenu.Add(nextDelayMin);
        _thunderstormSettingsMenu.Add(nextDelayMax);

        AddSaveAndResetButtons(
            _thunderstormSettingsMenu,
            resetAction: () =>
            {
                enableThunderInterference.Checked = true;

                pendingInitialMin = 5.0f;
                pendingInitialMax = 15.0f;

                pendingDurationMin = 0.5f;
                pendingDurationMax = 2.5f;

                pendingNextMin = 10.0f;
                pendingNextMax = 30.0f;

                initialDelayMin.Title = $"Initial Delay Min ({pendingInitialMin} s)";
                initialDelayMax.Title = $"Initial Delay Max ({pendingInitialMax} s)";

                durationMin.Title = $"Interference Duration Min ({pendingDurationMin} s)";
                durationMax.Title = $"Interference Duration Max ({pendingDurationMax} s)";

                nextDelayMin.Title = $"Next Delay Min ({pendingNextMin} s)";
                nextDelayMax.Title = $"Next Delay Max ({pendingNextMax} s)";
            },
            saveAction: () =>
            {
                thunderCfg.EnableThunderstormInterference = enableThunderInterference.Checked;

                thunderCfg.InitialInterferenceDelayMin = pendingInitialMin;
                thunderCfg.InitialInterferenceDelayMax = pendingInitialMax;

                thunderCfg.InterferenceDurationMin = pendingDurationMin;
                thunderCfg.InterferenceDurationMax = pendingDurationMax;

                thunderCfg.NextInterferenceDelayMin = pendingNextMin;
                thunderCfg.NextInterferenceDelayMax = pendingNextMax;

                FileOperations.SaveSettingsSection(
                    thunderCfg,
                    cfg => cfg.RealisticVehicleRadio.Thunderstorm,
                    (cfg, settings) => cfg.RealisticVehicleRadio.Thunderstorm = settings
                );
            }
        );

        _realisticVehicleRadioSettingsMenu.AddSubMenu(_thunderstormSettingsMenu, "").Title = "Thunderstorm Interference";
    }

    // ==========================================
    // HELPER METHODS TO REDUCE REPETITION
    // ==========================================
    private void BindFloatInput(NativeItem item, Action<float> setter, string titleTemplate, string errorMsg, Func<float, bool> validator = null)
    {
        item.Activated += (s, e) =>
        {
            string userInput = GUI.PromptUserForInput(true);
            if (float.TryParse(userInput, out float value) && (validator == null || validator(value)))
            {
                setter(value);
                item.Title = string.Format(titleTemplate, value);
            }
            else
            {
                string settingName = item.Title.Contains("(") ? item.Title.Split('(')[0].Trim() : item.Title;
                Notification.PostTicker($"~w~Invalid ~r~{settingName}~w~ input. {errorMsg}", true);
            }
        };
    }

    private void BindIntInput(NativeItem item, Action<int> setter, string titleTemplate, string errorMsg, Func<int, bool> validator = null)
    {
        item.Activated += (s, e) =>
        {
            string userInput = GUI.PromptUserForInput(true);
            if (int.TryParse(userInput, out int value) && (validator == null || validator(value)))
            {
                setter(value);
                item.Title = string.Format(titleTemplate, value);
            }
            else
            {
                string settingName = item.Title.Contains("(") ? item.Title.Split('(')[0].Trim() : item.Title;
                Notification.PostTicker($"~w~Invalid ~r~{settingName}~w~ input. {errorMsg}", true);
            }
        };
    }

    private void AddSaveAndResetButtons(NativeMenu menu, Action resetAction, Action saveAction)
    {
        NativeItem restToDefaultBtn = new NativeItem("~b~Reset to Defaults", "Resets all options within this menu to default. ~y~Press the save button to apply changes.");
        restToDefaultBtn.Activated += (s, i) => resetAction();

        NativeItem saveBtn = new NativeItem("~g~Save Changes");
        saveBtn.Activated += (s, i) => saveAction();

        menu.Add(restToDefaultBtn);
        menu.Add(saveBtn);
    }

    private void BindKeyAssignment(NativeMenu menu, NativeListItem<string> menuItem, PendingKeyBind settingName, string displayName)
    {
        menuItem.Activated += (sender, item) =>
        {
            FullOverlayText = $"~y~KEY BINDING~w~~n~Press a key to assign~n~~b~{displayName}";
            TypedOverlayText = string.Empty;
            TypingIndex = 0;
            LastTypeTime = Game.GameTime;

            WaitingForKeyAssignment = true;
            PendingKey = settingName;
            PendingListItem = menuItem;
            PendingMenu = menu;
        };
    }

    public static string GetTypingText()
    {
        if (TypingIndex >= FullOverlayText.Length) return FullOverlayText;

        if (Game.GameTime - LastTypeTime >= TYPE_SPEED)
        {
            LastTypeTime = Game.GameTime;
            TypingIndex++;

            if (TypingIndex < FullOverlayText.Length && FullOverlayText[TypingIndex - 1] == '~')
            {
                while (TypingIndex < FullOverlayText.Length && FullOverlayText[TypingIndex] != '~') TypingIndex++;
                if (TypingIndex < FullOverlayText.Length) TypingIndex++;
            }

            TypedOverlayText = FullOverlayText.Substring(0, TypingIndex);
        }

        return TypedOverlayText;
    }

    public static void DrawFadeOverlay(string text)
    {
        float t = Alpha / 170f;
        t = t * t * (3f - 2f * t);
        int easedAlpha = (int)(t * 170);

        Function.Call(Hash.DRAW_RECT, 0.5f, 0.5f, 1.0f, 1.0f, 0, 0, 0, easedAlpha);

        int textAlpha = WaitingForKeyAssignment ? (int)(Math.Pow(Alpha / 170f, 0.75f) * 170) : easedAlpha;

        Function.Call(Hash.SET_TEXT_FONT, 0);
        Function.Call(Hash.SET_TEXT_SCALE, 0.5f, 0.45f);
        Function.Call(Hash.SET_TEXT_COLOUR, 255, 255, 255, textAlpha);
        Function.Call(Hash.SET_TEXT_CENTRE, true);
        Function.Call(Hash.SET_TEXT_OUTLINE);
        Function.Call(Hash.SET_TEXT_DROP_SHADOW);
        Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "STRING");
        Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, text);
        Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, 0.5f, 0.5f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetKeyIndex(string key)
    {
        Enum.TryParse(key, true, out Keys parsedKey);
        return Array.IndexOf(KeysNames, parsedKey.ToString());
    }

    public enum PendingKeyBind
    {
        None,
        Debug,
        Record,
        OpenMenu
    }
}