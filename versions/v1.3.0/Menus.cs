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
using System.Globalization;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using LemonUI;
using LemonUI.Menus;
using static RealisticVehicleRadio.Helpers;

using GTA;
using GTA.Math;

namespace RealisticVehicleRadio
{
    public sealed class Menus
    {
        public static readonly ObjectPool UiMenuPool = new ObjectPool();
        public readonly NativeMenu MainMenu = GUI.CreateAndRegisterMenu(UiMenuPool, "Realistic Vehicle Radio", $"Main Menu {UpdateChecker.CURRENT_SCRIPT_VERSION}");

        public static bool WaitingForKeyAssignment = false;
        public static NativeMenu PendingMenu = null;
        public static NativeListItem<string> PendingListItem = null;

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
        public NativeMenu _performanceSettingsMenu;
        private NativeMenu _manageTunnelsMenu;
        private NativeMenu _editTunnelMenu;

        private int _selectedTunnelId = -1;
        public static bool IsManualRecordingActive => TunnelRecorder.EnableManualRecording;

        private readonly Action _onExclusionsChanged;

        private bool _resetAllSettings;

        public Menus(Configuration cfg, Configuration.MainSettings mainCfg, Configuration.PerformanceSettings perfCfg, Configuration.LoudnessSettings loudnessCfg, Configuration.TunnelDetectionSettings tunnelCfg, Configuration.VehicleExclusions exclusionsCfg, Configuration.TunnelDebugVisualizerSettings debugCfg, Configuration.TunnelRecorderSettings recorderCfg, Configuration.ThunderstormSettings thunderCfg, Action onExclusionsChanged)
        {
            _onExclusionsChanged = onExclusionsChanged;

            InitializeMainTreeSettingsMenu();
            InitializeRadioSettingsMenu();
            InitializeDebugSettingsMenu(debugCfg);
            InitializeManualRecorderSettingsMenu(recorderCfg);
            InitializeRecorderSettingsMenu(recorderCfg);
            InitializePerformanceSettingsMenu(perfCfg);
            InitializeMainRadioSettingsMenu(mainCfg);
            InitializeLoudnessSettingsMenu(loudnessCfg);
            InitializeThunderstormSettingsMenu(thunderCfg);
            InitializeTunnelSettingsMenu(tunnelCfg);
            InitializeExclusionsSettingsMenu(exclusionsCfg);
            InitializeTunnelManagerMenu();

            AddResetAllSettingsToDefaultBtn(cfg);

            TunnelManager.TunnelsChanged += RefreshTunnelManagerMenuIfVisible;
        }

        private void InitializeMainTreeSettingsMenu()
        {
            _mainSettingsTreeMenu = GUI.CreateAndRegisterMenu(
                UiMenuPool,
                "Settings",
                "Configure Global Mod Preferences"
            );

            MainMenu.AddSubMenu(_mainSettingsTreeMenu, "").Title = "Settings";
        }

        private void InitializeRadioSettingsMenu()
        {
            _realisticVehicleRadioSettingsMenu = GUI.CreateAndRegisterMenu(
                UiMenuPool,
                "Radio Settings",
                "Adjust Core Radio Mechanics & Behaviors"
            );

            _mainSettingsTreeMenu.AddSubMenu(_realisticVehicleRadioSettingsMenu, "").Title = "Radio Settings";
        }

        private void InitializeDebugSettingsMenu(Configuration.TunnelDebugVisualizerSettings debugCfg)
        {
            TunnelDebugVisualizerSettingsMenu = GUI.CreateAndRegisterMenu(
                UiMenuPool,
                "Debug Settings",
                "Toggle Tunnel Visualizer"
            );

            float pendingMaxDistance = debugCfg.MaxDrawDistance;

            NativeItem maxDrawDistance = new NativeItem(
                $"Max Draw Distance ({pendingMaxDistance} m)",
                "Adjust how far, in meters, the script draws the tunnel visualizer. " +
                "~y~Minimum value is 10 m. Values below 10 are automatically increased to 10. Press the save button to apply the changes."
            );

            BindFloatInput(
                maxDrawDistance,
                val => pendingMaxDistance = val,
                val => Math.Max(10f, val),
                "Max Draw Distance ({0} m)",
                "Enter a value of 10 or greater."
            );

            NativeListItem<string> toggleDebugKeyItem = GUI.CreateEnumListItem<Keys>(
                "Toggle Debug Key",
                false,
                GetKeyIndex(debugCfg.DebugToggleKey),
                "Choose the key to toggle debug mode. ~y~Either scroll through the list or press enter then press the key you want to assign and press the save button to apply changes."
            );
            BindKeyAssignment(
                TunnelDebugVisualizerSettingsMenu,
                toggleDebugKeyItem,
                "Toggle Debug Key"
            );

            TunnelDebugVisualizerSettingsMenu.Add(maxDrawDistance);
            TunnelDebugVisualizerSettingsMenu.Add(toggleDebugKeyItem);

            AddSaveAndResetButtons(
                TunnelDebugVisualizerSettingsMenu,
                resetAction: () =>
                {
                    pendingMaxDistance = 150f;
                    maxDrawDistance.Title = $"Max Draw Distance ({pendingMaxDistance} m)";
                    toggleDebugKeyItem.SelectedItem = "F6";
                },
                saveAction: async () =>
                {
                    debugCfg.MaxDrawDistance = pendingMaxDistance;
                    debugCfg.DebugToggleKey = toggleDebugKeyItem.SelectedItem;

                    await FileOperations.SaveSettingsSectionAsync(
                        debugCfg,
                        cfg => cfg.TunnelDebugVisualizer,
                        (cfg, settings) => cfg.TunnelDebugVisualizer = settings
                    );
                }
            );

            TunnelDebugVisualizerSettingsMenu.Opening += (s, e) =>
            {
                pendingMaxDistance = debugCfg.MaxDrawDistance;
                maxDrawDistance.Title = $"Max Draw Distance ({pendingMaxDistance} m)";
                toggleDebugKeyItem.SelectedItem = debugCfg.DebugToggleKey;
            };

            _mainSettingsTreeMenu.AddSubMenu(TunnelDebugVisualizerSettingsMenu, "").Title = "Debug Settings";
        }

        private void InitializeRecorderSettingsMenu(Configuration.TunnelRecorderSettings recorderCfg)
        {
            _tunnelRecorderSettingsMenu = GUI.CreateAndRegisterMenu(
                UiMenuPool,
                "Tunnel Recorder",
                "Record & Export Tunnel Coordinates"
            );

            string pendingTunnelName = TunnelRecorder.PendingTunnelName;
            float pendingMinDistanceMeters = recorderCfg.MinDistanceMeters;
            float pendingHeadingDeltaThresholdDeg = recorderCfg.HeadingDeltaThresholdDeg;
            float pendingMaxHorizontalSearch = recorderCfg.MaxHorizontalSearch;
            float pendingMaxVerticalSearch = recorderCfg.MaxVerticalSearch;
            float pendingWallRayHeightOffset = recorderCfg.WallRayHeightOffset;

            NativeListItem<string> toggleRecordingKeyItem = GUI.CreateEnumListItem<Keys>(
                "Recorder Debug Key",
                false,
                GetKeyIndex(recorderCfg.RecordToggleKey),
                "Choose the key to toggle recording mode. ~y~Either scroll through the list or press enter then press the key you want to assign and press the save button to apply changes."
            );
            BindKeyAssignment(
                _tunnelRecorderSettingsMenu,
                toggleRecordingKeyItem,
                "Recorder Debug Key"
            );

            NativeItem minDistanceMeters = new NativeItem(
                $"Min Distance ({pendingMinDistanceMeters} m)",
                "Minimum distance the vehicle must travel before recording a new tunnel point. ~y~Higher values = fewer points (less detail). Press the save button to apply the changes."
            );
            NativeItem headingDeltaThresholdDeg = new NativeItem(
                $"Heading Delta Threshold ({pendingHeadingDeltaThresholdDeg} °)",
                "Minimum change in vehicle direction (in degrees) to force a new tunnel point. ~y~Ensures sharp turns are captured accurately. Press the save button to apply the changes."
            );
            NativeItem maxHorizontalSearch = new NativeItem(
                $"Max Horizontal Search ({pendingMaxHorizontalSearch} m)",
                "Maximum distance to search left and right for tunnel walls. ~y~Increase for very wide tunnels, decrease if it hits outside objects. Press the save button to apply the changes."
            );
            NativeItem maxVerticalSearch = new NativeItem(
                $"Max Vertical Search ({pendingMaxVerticalSearch} m)",
                "Maximum distance to search up and down for the tunnel ceiling and floor. ~y~Increase if the tunnel is exceptionally tall. Press the save button to apply the changes."
            );
            NativeItem wallRayHeightOffset = new NativeItem(
                $"Wall Ray Height Offset ({pendingWallRayHeightOffset} m)",
                "Vertical offset from the vehicle where the wall-detecting rays start. ~y~Adjust if the rays are hitting the ground or the vehicle itself. Press the save button to apply the changes."
            );
            NativeItem nextTunnelNameItem = new NativeItem(
                $"Next Tunnel Name: {pendingTunnelName}",
                "Set a custom name for the next tunnel you record. ~y~Click to type a name. Press the save button to apply the changes."
            );

            nextTunnelNameItem.Activated += (s, e) =>
            {
                string userInput = GUI.PromptUserForInput(false);

                if (userInput != null)
                {
                    pendingTunnelName = userInput;
                    nextTunnelNameItem.Title = $"Next Tunnel Name: {pendingTunnelName}";
                }
            };

            BindFloatInput(
                minDistanceMeters,
                val => pendingMinDistanceMeters = val,
                val => Math.Max(0f, val),
                "Min Distance ({0} m)",
                "Enter a value of 0 or greater."
            );
            BindFloatInput(
                headingDeltaThresholdDeg,
                val => pendingHeadingDeltaThresholdDeg = val,
                val => Math.Max(0f, val),
                "Heading Delta Threshold ({0} °)",
                "Enter a value of 0 or greater."
            );
            BindFloatInput(
                maxHorizontalSearch,
                val => pendingMaxHorizontalSearch = val,
                val => Math.Max(0f, val),
                "Max Horizontal Search ({0} m)",
                "Enter a value of 0 or greater."
            );
            BindFloatInput(
                maxVerticalSearch,
                val => pendingMaxVerticalSearch = val,
                val => Math.Max(0f, val),
                "Max Vertical Search ({0} m)",
                "Enter a value of 0 or greater."
            );
            BindFloatInput(
                wallRayHeightOffset,
                val => pendingWallRayHeightOffset = val,
                val => MathFunctions.Clamp(val, float.MinValue, float.MaxValue),
                "Wall Ray Height Offset ({0} m)",
                "Enter a numeric value"
            );

            _tunnelRecorderSettingsMenu.Add(nextTunnelNameItem);
            _tunnelRecorderSettingsMenu.Add(minDistanceMeters);
            _tunnelRecorderSettingsMenu.Add(headingDeltaThresholdDeg);
            _tunnelRecorderSettingsMenu.Add(maxHorizontalSearch);
            _tunnelRecorderSettingsMenu.Add(maxVerticalSearch);
            _tunnelRecorderSettingsMenu.Add(wallRayHeightOffset);
            _tunnelRecorderSettingsMenu.Add(toggleRecordingKeyItem);

            _tunnelRecorderSettingsMenu.AddSubMenu(ManualRecordingMenu).Title = "~y~Manual Recording";

            AddSaveAndResetButtons(
                _tunnelRecorderSettingsMenu,
                resetAction: () =>
                {
                    pendingMinDistanceMeters = 1.0f; minDistanceMeters.Title = $"Min Distance ({pendingMinDistanceMeters} m)";
                    pendingHeadingDeltaThresholdDeg = 5.0f; headingDeltaThresholdDeg.Title = $"Heading Delta Threshold ({pendingHeadingDeltaThresholdDeg} °)";
                    pendingMaxHorizontalSearch = 30.0f; maxHorizontalSearch.Title = $"Max Horizontal Search ({pendingMaxHorizontalSearch} m)";
                    pendingMaxVerticalSearch = 30.0f; maxVerticalSearch.Title = $"Max Vertical Search ({pendingMaxVerticalSearch} m)";
                    pendingWallRayHeightOffset = 0.5f; wallRayHeightOffset.Title = $"Wall Ray Height Offset ({pendingWallRayHeightOffset} m)";
                    toggleRecordingKeyItem.SelectedItem = "F7";

                    // Stage the default name; do NOT touch TunnelRecorder.PendingTunnelName here.
                    pendingTunnelName = "New Tunnel";
                    nextTunnelNameItem.Title = $"Next Tunnel Name: {pendingTunnelName}";
                },
                saveAction: async () =>
                {
                    recorderCfg.MinDistanceMeters = pendingMinDistanceMeters;
                    recorderCfg.HeadingDeltaThresholdDeg = pendingHeadingDeltaThresholdDeg;
                    recorderCfg.MaxHorizontalSearch = pendingMaxHorizontalSearch;
                    recorderCfg.MaxVerticalSearch = pendingMaxVerticalSearch;
                    recorderCfg.WallRayHeightOffset = pendingWallRayHeightOffset;
                    recorderCfg.RecordToggleKey = toggleRecordingKeyItem.SelectedItem;

                    // Commit the staged tunnel name.
                    TunnelRecorder.PendingTunnelName = pendingTunnelName;
                    await FileOperations.SaveSettingsSectionAsync(
                    recorderCfg,
                    cfg => cfg.TunnelRecorder,
                    (cfg, settings) => cfg.TunnelRecorder = settings
                );
                }
            );

            _tunnelRecorderSettingsMenu.Opening += (s, e) =>
            {
                pendingTunnelName = TunnelRecorder.PendingTunnelName;
                nextTunnelNameItem.Title = $"Next Tunnel Name: {pendingTunnelName}";

                pendingMinDistanceMeters = recorderCfg.MinDistanceMeters;
                minDistanceMeters.Title = $"Min Distance ({pendingMinDistanceMeters} m)";

                pendingHeadingDeltaThresholdDeg = recorderCfg.HeadingDeltaThresholdDeg;
                headingDeltaThresholdDeg.Title = $"Heading Delta Threshold ({pendingHeadingDeltaThresholdDeg} °)";

                pendingMaxHorizontalSearch = recorderCfg.MaxHorizontalSearch;
                maxHorizontalSearch.Title = $"Max Horizontal Search ({pendingMaxHorizontalSearch} m)";

                pendingMaxVerticalSearch = recorderCfg.MaxVerticalSearch;
                maxVerticalSearch.Title = $"Max Vertical Search ({pendingMaxVerticalSearch} m)";

                pendingWallRayHeightOffset = recorderCfg.WallRayHeightOffset;
                wallRayHeightOffset.Title = $"Wall Ray Height Offset ({pendingWallRayHeightOffset} m)";

                toggleRecordingKeyItem.SelectedItem = recorderCfg.RecordToggleKey;
            };

            _mainSettingsTreeMenu.AddSubMenu(_tunnelRecorderSettingsMenu, "").Title = "Tunnel Recorder";
        }

        private void InitializeManualRecorderSettingsMenu(Configuration.TunnelRecorderSettings recorderCfg)
        {
            ManualRecordingMenu = GUI.CreateAndRegisterMenu(
                UiMenuPool,
                "Manual Recording",
                "Manually add, remove, and save tunnel points"
            );

            // New in V1.3.0: Gamplay camera now move to let you see the raycast hit
            ManualRecordingMenu.MouseBehavior = MenuMouseBehavior.Disabled;

            NativeCheckboxItem enableManualRecording = GUI.CreateCheckboxItem(
                "Enable Manual Recording",
                TunnelRecorder.EnableManualRecording,
                "Toggles manual recording mode. When enabled, preview lines are drawn around your vehicle (requires Debug mode) and the automatic recording key is disabled."
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
                    GUI.ShowContextualMessage("~r~You must be in a vehicle to add a point.");
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
        }

        private void InitializeMainRadioSettingsMenu(Configuration.MainSettings mainCfg)
        {
            _mainSettingsMenu = GUI.CreateAndRegisterMenu(
                UiMenuPool,
                "Main Settings",
                "General Configuration Options"
            );

            float pendingRadioDisableDepth = mainCfg.RadioDisableDepth;

            NativeListItem<string> openMenuKeyItem = GUI.CreateEnumListItem<Keys>(
                "Open Menu Key",
                false,
                GetKeyIndex(mainCfg.OpenMenuKey),
                "Choose the key to open main menu. ~y~Either scroll through the list or press enter then press the key you want to assign and press the save button to apply changes."
            );
            BindKeyAssignment(
                _mainSettingsMenu,
                openMenuKeyItem,
                "Open Menu Key"
            );

            NativeItem radioDisableDepthDistance = new NativeItem(
                $"Radio Disable Depth ({pendingRadioDisableDepth} m)",
                "Determines how deep a vehicle must be underwater before its radio stops working. Higher values allow the radio to continue playing longer while submerged. ~y~Press the save button to apply the changes."
            );
            BindFloatInput(
                radioDisableDepthDistance,
                val => pendingRadioDisableDepth = val,
                val => Math.Max(0f, val),
                "Radio Disable Depth ({0} m)",
                "Enter a value of 0 or greater."
            );

            NativeCheckboxItem muteRadioInTunnels = GUI.CreateCheckboxItem(
                "Mute Radio In Tunnels",
                mainCfg.MuteRadioInTunnels,
                "If enabled, radios will cut out in the middle of tunnels. ~y~If \"Static Radio In Tunnels\" is also enabled, this creates the full static-to-mute effect. Press the save button to apply changes."
            );

            NativeCheckboxItem staticRadioInTunnels = GUI.CreateCheckboxItem(
                "Static Radio In Tunnels",
                mainCfg.EnableStaticRadioInTunnels,
                "If enabled, the radio signal degrades with static when entering/exiting tunnels. ~y~If disabled, tunnels will either instantly mute the radio or do nothing if \"Mute Radio In Tunnels\" is disabled. Press the save button to apply changes."
            );

            NativeCheckboxItem disableRadioInWater = GUI.CreateCheckboxItem(
                "Disable Radio In Water",
                mainCfg.DisableRadioInWater,
                "If enabled, the vehicle’s radio will mute when the vehicle is underwater at a depth equal to or greater than the \"Radio Disable Depth\". ~y~Press the save button to apply changes."
            );

            NativeCheckboxItem autoCheckUpdatesItem = GUI.CreateCheckboxItem(
                "Auto-check for updates on startup",
                mainCfg.AutoCheckForUpdatesOnStartup,
                "Automatically checks for script updates when the game starts. ~y~Press the save button to apply changes."
            );

            NativeItem checkUpdatesBtn = new NativeItem("~b~Check for Updates Now");
            checkUpdatesBtn.Activated += (s, i) =>
            {
                Task.Run(async () => await UpdateChecker.CheckForUpdatesAsync());
            };

            _mainSettingsMenu.Add(radioDisableDepthDistance);
            _mainSettingsMenu.Add(muteRadioInTunnels);
            _mainSettingsMenu.Add(staticRadioInTunnels);
            _mainSettingsMenu.Add(disableRadioInWater);
            _mainSettingsMenu.Add(autoCheckUpdatesItem);
            _mainSettingsMenu.Add(openMenuKeyItem);
            _mainSettingsMenu.AddSubMenu(_performanceSettingsMenu).Title = "~y~Performance Settings";
            _mainSettingsMenu.Add(checkUpdatesBtn);
            AddSaveAndResetButtons(
                _mainSettingsMenu,
                resetAction: () =>
                {
                    pendingRadioDisableDepth = 10f;
                    radioDisableDepthDistance.Title = $"Radio Disable Depth ({pendingRadioDisableDepth} m)";
                    muteRadioInTunnels.Checked = true;
                    staticRadioInTunnels.Checked = true;
                    disableRadioInWater.Checked = true;
                    autoCheckUpdatesItem.Checked = true;
                    openMenuKeyItem.SelectedItem = "F3";
                },
                saveAction: async () =>
                {
                    mainCfg.RadioDisableDepth = pendingRadioDisableDepth;
                    mainCfg.MuteRadioInTunnels = muteRadioInTunnels.Checked;
                    mainCfg.EnableStaticRadioInTunnels = staticRadioInTunnels.Checked;
                    mainCfg.DisableRadioInWater = disableRadioInWater.Checked;
                    mainCfg.AutoCheckForUpdatesOnStartup = autoCheckUpdatesItem.Checked;
                    mainCfg.OpenMenuKey = openMenuKeyItem.SelectedItem;
                    await FileOperations.SaveSettingsSectionAsync(
                        mainCfg,
                        cfg => cfg.RealisticVehicleRadio.Main,
                        (cfg, settings) => cfg.RealisticVehicleRadio.Main = settings
                    );
                }
            );

            _mainSettingsMenu.Opening += (s, e) =>
            {
                pendingRadioDisableDepth = mainCfg.RadioDisableDepth;
                radioDisableDepthDistance.Title = $"Radio Disable Depth ({pendingRadioDisableDepth} m)";

                muteRadioInTunnels.Checked = mainCfg.MuteRadioInTunnels;
                staticRadioInTunnels.Checked = mainCfg.EnableStaticRadioInTunnels;
                disableRadioInWater.Checked = mainCfg.DisableRadioInWater;
                autoCheckUpdatesItem.Checked = mainCfg.AutoCheckForUpdatesOnStartup;

                openMenuKeyItem.SelectedItem = mainCfg.OpenMenuKey;
            };

            _realisticVehicleRadioSettingsMenu.AddSubMenu(_mainSettingsMenu, "").Title = "Main Settings";
        }

        private void InitializePerformanceSettingsMenu(Configuration.PerformanceSettings perfCfg)
        {
            _performanceSettingsMenu = GUI.CreateAndRegisterMenu(
                UiMenuPool,
                "Performance Settings",
                "Tweak Script Performance & Caching Limits"
            );

            float pendingVehiclesSearchDistance = perfCfg.VehiclesSearchDistance;
            int pendingMaxProcessVehicles = perfCfg.MaxProcessVehicles;
            int pendingLogicUpdateIntervalMs = perfCfg.LogicUpdateIntervalMs;
            int pendingScanIntervalMs = perfCfg.ScanIntervalMs;
            int pendingMaxCachedVehicles = perfCfg.MaxCachedVehicles;
            float pendingMaxProcessDistance = perfCfg.MaxProcessDistance;
            float pendingRemoveDistance = perfCfg.RemoveDistance;

            NativeItem vehiclesSearchDistance = new NativeItem($"Vehicles Search Distance ({pendingVehiclesSearchDistance} m)");
            BindFloatInput(
                vehiclesSearchDistance,
                val => pendingVehiclesSearchDistance = val,
                val => Math.Max(0f, val),
                "Vehicles Search Distance ({0} m)",
                "Enter a value of 0 or greater."
            );

            NativeItem maxProcessVehicles = new NativeItem($"Max Process Vehicles ({pendingMaxProcessVehicles})");
            BindIntInput(
                maxProcessVehicles,
                val => pendingMaxProcessVehicles = val,
                val => Math.Max(0, val),
                "Max Process Vehicles ({0})",
                "Enter a value of 0 or greater."
            );

            NativeItem logicUpdateInterval = new NativeItem(
                $"Logic Update Interval ({pendingLogicUpdateIntervalMs} ms)",
                "How often the heavy vehicle logic (loud radio, radio mute, etc.) runs. ~y~Minimum value is 100 ms. Values below 100 are automatically increased to 100. Press the save button to apply changes."
            );

            BindIntInput(
                logicUpdateInterval,
                val => pendingLogicUpdateIntervalMs = val,
                val => Math.Max(100, val),
                "Logic Update Interval ({0} ms)",
                "Enter a value of 100 or greater."
            );

            NativeItem scanInterval = new NativeItem(
                $"Scan Interval ({pendingScanIntervalMs} ms)",
                "How often the script scans the world for newly spawned vehicles. ~y~Minimum value is 100 ms. Values below 100 are automatically increased to 100. Press the save button to apply changes."
            );

            BindIntInput(
                scanInterval,
                val => pendingScanIntervalMs = val,
                val => Math.Max(100, val),
                "Scan Interval ({0} ms)",
                "Enter a value of 100 or greater."
            );

            NativeItem maxCachedVehicles = new NativeItem($"Max Tracked Vehicles ({pendingMaxCachedVehicles})");
            BindIntInput(
                maxCachedVehicles,
                val => pendingMaxCachedVehicles = val,
                val => Math.Max(0, val),
                "Max Tracked Vehicles ({0})",
                "Enter a value of 0 or greater."
            );

            NativeItem maxProcessDistance = new NativeItem($"Max Process Distance ({pendingMaxProcessDistance} m)");
            BindFloatInput(
                maxProcessDistance,
                val => pendingMaxProcessDistance = val,
                val => Math.Max(0f, val),
                "Max Process Distance ({0} m)",
                "Enter a value of 0 or greater."
            );

            NativeItem removeDistance = new NativeItem($"Remove Distance ({pendingRemoveDistance} m)");
            BindFloatInput(
                removeDistance,
                val => pendingRemoveDistance = val,
                val => Math.Max(0f, val),
                "Remove Distance ({0} m)",
                "Enter a value of 0 or greater."
            );

            // Index in UpdatePerformanceMenuStats
            _performanceSettingsMenu.Add(vehiclesSearchDistance); //0
            _performanceSettingsMenu.Add(scanInterval); //1
            _performanceSettingsMenu.Add(maxProcessVehicles); //2
            _performanceSettingsMenu.Add(logicUpdateInterval); //3
            _performanceSettingsMenu.Add(maxProcessDistance); //4
            _performanceSettingsMenu.Add(maxCachedVehicles); //5
            _performanceSettingsMenu.Add(removeDistance); //6

            AddSaveAndResetButtons(
                _performanceSettingsMenu,
                resetAction: () =>
                {
                    pendingVehiclesSearchDistance = 50f;
                    pendingMaxProcessVehicles = 30;
                    pendingLogicUpdateIntervalMs = 100;
                    pendingScanIntervalMs = 250;
                    pendingMaxCachedVehicles = 48;
                    pendingMaxProcessDistance = 60f;
                    pendingRemoveDistance = 100f;

                    vehiclesSearchDistance.Title = $"Vehicles Search Distance ({pendingVehiclesSearchDistance} m)";
                    maxProcessVehicles.Title = $"Max Process Vehicles ({pendingMaxProcessVehicles})";
                    logicUpdateInterval.Title = $"Logic Update Interval ({pendingLogicUpdateIntervalMs} ms)";
                    scanInterval.Title = $"Scan Interval ({pendingScanIntervalMs} ms)";
                    maxCachedVehicles.Title = $"Max Tracked Vehicles ({pendingMaxCachedVehicles})";
                    maxProcessDistance.Title = $"Max Process Distance ({pendingMaxProcessDistance} m)";
                    removeDistance.Title = $"Remove Distance ({pendingRemoveDistance} m)";
                },
                saveAction: async () =>
                {
                    perfCfg.VehiclesSearchDistance = pendingVehiclesSearchDistance;
                    perfCfg.MaxProcessVehicles = pendingMaxProcessVehicles;
                    perfCfg.LogicUpdateIntervalMs = pendingLogicUpdateIntervalMs;
                    perfCfg.ScanIntervalMs = pendingScanIntervalMs;
                    perfCfg.MaxCachedVehicles = pendingMaxCachedVehicles;
                    perfCfg.MaxProcessDistance = pendingMaxProcessDistance;
                    perfCfg.RemoveDistance = pendingRemoveDistance;

                    await FileOperations.SaveSettingsSectionAsync(
                        perfCfg,
                        cfg => cfg.RealisticVehicleRadio.Performance,
                        (cfg, settings) => cfg.RealisticVehicleRadio.Performance = settings
                    );
                }
            );

            _performanceSettingsMenu.Opening += (s, e) =>
            {
                pendingVehiclesSearchDistance = perfCfg.VehiclesSearchDistance;
                vehiclesSearchDistance.Title = $"Vehicles Search Distance ({pendingVehiclesSearchDistance} m)";

                pendingMaxProcessVehicles = perfCfg.MaxProcessVehicles;
                maxProcessVehicles.Title = $"Max Process Vehicles ({pendingMaxProcessVehicles})";

                pendingLogicUpdateIntervalMs = perfCfg.LogicUpdateIntervalMs;
                logicUpdateInterval.Title = $"Logic Update Interval ({pendingLogicUpdateIntervalMs} ms)";

                pendingScanIntervalMs = perfCfg.ScanIntervalMs;
                scanInterval.Title = $"Scan Interval ({pendingScanIntervalMs} ms)";

                pendingMaxCachedVehicles = perfCfg.MaxCachedVehicles;
                maxCachedVehicles.Title = $"Max Tracked Vehicles ({pendingMaxCachedVehicles})";

                pendingMaxProcessDistance = perfCfg.MaxProcessDistance;
                maxProcessDistance.Title = $"Max Process Distance ({pendingMaxProcessDistance} m)";

                pendingRemoveDistance = perfCfg.RemoveDistance;
                removeDistance.Title = $"Remove Distance ({pendingRemoveDistance} m)";
            };
        }

        private void InitializeLoudnessSettingsMenu(Configuration.LoudnessSettings loudnessCfg)
        {
            _loudnessSettingsMenu = GUI.CreateAndRegisterMenu(
                UiMenuPool,
                "Radio Loudness",
                "Manage Loud Radio Settings"
            );

            int pendingRandomLoudRadioChance = loudnessCfg.RandomLoudRadioChancePercentage;
            int pendingLoudEchoVehicleCount = loudnessCfg.LoudEchoVehicleCount;

            NativeItem randomLoudRadioChancePercentage = new NativeItem(
                $"Random Loud Radio Chance ({pendingRandomLoudRadioChance} %)",
                "Adjust the chances of getting a loud radio in a vehicle after all other checks. ~y~Press the save button to apply changes."
            );
            BindIntInput(
                randomLoudRadioChancePercentage,
                val => pendingRandomLoudRadioChance = val,
                val => MathFunctions.Clamp(val, 0, 100),
                "Random Loud Radio Chance ({0} %)",
                "Enter a percentage between 0 and 100."
            );

            NativeItem loudEchoVehicleCount = new NativeItem(
                $"Radio Amplifier Vehicles ({pendingLoudEchoVehicleCount})",
                "Spawns invisible vehicles attached to your last vehicle to amplify the radio volume.~y~ 0 disables this feature. Higher count = louder radio. Only affects the player's last vehicles. Press save to apply changes."
            );
            BindIntInput(
                loudEchoVehicleCount,
                val => pendingLoudEchoVehicleCount = val,
                val => Math.Max(0, val),
                "Radio Amplifier Vehicles ({0})",
                "Enter a value of 0 or greater."
            );

            NativeCheckboxItem enableRoofCheck = GUI.CreateCheckboxItem(
                "Enable Roof Check",
                loudnessCfg.EnableRoofCheck,
                "Automatically sets radio to loud when the convertible top or sunroof is open or opening. ~y~Press the save button to apply changes."
            );

            NativeCheckboxItem enableWindowCheck = GUI.CreateCheckboxItem(
                "Enable Window Check",
                loudnessCfg.EnableWindowCheck,
                "Automatically sets radio to loud if any window in the vehicle is broken or shattered. ~y~Press the save button to apply changes."
            );

            NativeCheckboxItem enableDoorCheck = GUI.CreateCheckboxItem(
                "Enable Door Check",
                loudnessCfg.EnableDoorCheck,
                "Automatically sets radio to loud if any door is open, ajar, or broken off. ~y~Press the save button to apply changes."
            );

            NativeCheckboxItem enableRandomChance = GUI.CreateCheckboxItem(
                "Enable Random Chance",
                loudnessCfg.EnableRandomChance,
                "Gives fully closed vehicles a random chance to have a loud radio. ~y~Vehicles with open roofs, broken windows, or open doors are guaranteed to be loud and skip this roll. Press the save button to apply changes."
            );

            _loudnessSettingsMenu.Add(enableRoofCheck);
            _loudnessSettingsMenu.Add(enableWindowCheck);
            _loudnessSettingsMenu.Add(enableDoorCheck);
            _loudnessSettingsMenu.Add(enableRandomChance);
            _loudnessSettingsMenu.Add(randomLoudRadioChancePercentage);
            _loudnessSettingsMenu.Add(loudEchoVehicleCount);

            AddSaveAndResetButtons(
                _loudnessSettingsMenu,
                resetAction: () =>
                {
                    pendingRandomLoudRadioChance = 50;
                    pendingLoudEchoVehicleCount = 0;
                    randomLoudRadioChancePercentage.Title = $"Random Loud Radio Chance ({pendingRandomLoudRadioChance} %)";
                    loudEchoVehicleCount.Title = $"Radio Amplifier Vehicles ({pendingLoudEchoVehicleCount})";
                    enableRoofCheck.Checked = true;
                    enableWindowCheck.Checked = true;
                    enableDoorCheck.Checked = true;
                    enableRandomChance.Checked = true;
                },
                saveAction: async () =>
                {
                    loudnessCfg.RandomLoudRadioChancePercentage = pendingRandomLoudRadioChance;
                    loudnessCfg.LoudEchoVehicleCount = pendingLoudEchoVehicleCount;
                    loudnessCfg.EnableRoofCheck = enableRoofCheck.Checked;
                    loudnessCfg.EnableWindowCheck = enableWindowCheck.Checked;
                    loudnessCfg.EnableDoorCheck = enableDoorCheck.Checked;
                    loudnessCfg.EnableRandomChance = enableRandomChance.Checked;
                    await FileOperations.SaveSettingsSectionAsync(
                        loudnessCfg,
                        cfg => cfg.RealisticVehicleRadio.Loudness,
                        (cfg, settings) => cfg.RealisticVehicleRadio.Loudness = settings
                    );
                }
            );

            _loudnessSettingsMenu.Opening += (s, e) =>
            {
                pendingRandomLoudRadioChance = loudnessCfg.RandomLoudRadioChancePercentage;
                randomLoudRadioChancePercentage.Title = $"Random Loud Radio Chance ({pendingRandomLoudRadioChance} %)";
                loudEchoVehicleCount.Title = $"Radio Amplifier Vehicles ({pendingLoudEchoVehicleCount})";

                enableRoofCheck.Checked = loudnessCfg.EnableRoofCheck;
                enableWindowCheck.Checked = loudnessCfg.EnableWindowCheck;
                enableDoorCheck.Checked = loudnessCfg.EnableDoorCheck;
                enableRandomChance.Checked = loudnessCfg.EnableRandomChance;
            };

            _realisticVehicleRadioSettingsMenu.AddSubMenu(_loudnessSettingsMenu, "").Title = "Radio Loudness";
        }

        private void InitializeTunnelSettingsMenu(Configuration.TunnelDetectionSettings tunnelCfg)
        {
            _tunnelDetectionSettingsMenu = GUI.CreateAndRegisterMenu(
                UiMenuPool,
                "Tunnel Detection",
                "Tweak Detection Radius"
            );

            float pendingTunnelHorizontalTolerance = tunnelCfg.TunnelHorizontalTolerance;
            float pendingTunnelVerticalTolerance = tunnelCfg.TunnelVerticalTolerance;

            NativeItem tunnelHorizontalTolerance = new NativeItem(
                $"Horizontal Tolerance ({pendingTunnelHorizontalTolerance} m)",
                "Adds extra horizontal leeway to the tunnel walls. Prevents the radio from cutting out if your recording is slightly off-center. ~y~Press the save button to apply changes."
            );
            NativeItem tunnelVerticalTolerance = new NativeItem(
                $"Vertical Tolerance ({pendingTunnelVerticalTolerance} m)",
                "Adds extra vertical space above the ceiling and below the floor. Useful for bumpy roads, ramps, or sloped tunnel entrances. ~y~Press the save button to apply changes."
            );

            BindFloatInput(
                tunnelHorizontalTolerance,
                val => pendingTunnelHorizontalTolerance = val,
                val => Math.Max(0f, val),
                "Horizontal Tolerance ({0} m)",
                "Enter a value of 0 or greater."
            );
            BindFloatInput(
                tunnelVerticalTolerance,
                val => pendingTunnelVerticalTolerance = val,
                val => Math.Max(0f, val),
                "Vertical Tolerance ({0} m)",
                "Enter a value of 0 or greater."
            );

            _tunnelDetectionSettingsMenu.Add(tunnelHorizontalTolerance);
            _tunnelDetectionSettingsMenu.Add(tunnelVerticalTolerance);

            AddSaveAndResetButtons(
                _tunnelDetectionSettingsMenu,
                resetAction: () =>
                {
                    pendingTunnelHorizontalTolerance = 0.2f;
                    tunnelHorizontalTolerance.Title = $"Horizontal Tolerance ({pendingTunnelHorizontalTolerance} m)";

                    pendingTunnelVerticalTolerance = 0.5f;
                    tunnelVerticalTolerance.Title = $"Vertical Tolerance ({pendingTunnelVerticalTolerance} m)";
                },
                saveAction: async () =>
                {
                    tunnelCfg.TunnelHorizontalTolerance = pendingTunnelHorizontalTolerance;
                    tunnelCfg.TunnelVerticalTolerance = pendingTunnelVerticalTolerance;

                    await FileOperations.SaveSettingsSectionAsync(
                        tunnelCfg,
                        cfg => cfg.RealisticVehicleRadio.TunnelDetection,
                        (cfg, settings) => cfg.RealisticVehicleRadio.TunnelDetection = settings
                    );

                    TunnelSpatialIndex.MarkDirty();
                }
            );

            _tunnelDetectionSettingsMenu.Opening += (s, e) =>
            {
                pendingTunnelHorizontalTolerance = tunnelCfg.TunnelHorizontalTolerance;
                tunnelHorizontalTolerance.Title = $"Horizontal Tolerance ({pendingTunnelHorizontalTolerance} m)";

                pendingTunnelVerticalTolerance = tunnelCfg.TunnelVerticalTolerance;
                tunnelVerticalTolerance.Title = $"Vertical Tolerance ({pendingTunnelVerticalTolerance} m)";
            };

            _realisticVehicleRadioSettingsMenu.AddSubMenu(_tunnelDetectionSettingsMenu, "").Title = "Tunnel Detection";
        }

        private void InitializeExclusionsSettingsMenu(Configuration.VehicleExclusions exclusionsCfg)
        {
            _exclusionsSettingsMenu = GUI.CreateAndRegisterMenu(
                UiMenuPool,
                "Excluded Vehicles",
                "Vehicles Exempt from Radio Logic"
            );

            NativeCheckboxItem disableRadioForMotorcycles = GUI.CreateCheckboxItem(
                "Disable Radio For Motorcycles",
                exclusionsCfg.DisableRadioForMotorcycles,
                "~y~Press the save button to apply changes."
            );
            NativeCheckboxItem disableRadioForPlanes = GUI.CreateCheckboxItem(
                "Disable Radio For Planes",
                exclusionsCfg.DisableRadioForPlanes,
                "~y~Press the save button to apply changes."
            );
            NativeCheckboxItem disableRadioForAircraft = GUI.CreateCheckboxItem(
                "Disable Radio For Aircraft",
                exclusionsCfg.DisableRadioForAircraft,
                "~y~Press the save button to apply changes."
            );
            NativeCheckboxItem disableRadioForQuadBikes = GUI.CreateCheckboxItem(
                "Disable Radio For Quad Bikes",
                exclusionsCfg.DisableRadioForQuadBikes,
                "~y~Press the save button to apply changes."
            );
            NativeCheckboxItem disableRadioForHelicopters = GUI.CreateCheckboxItem(
                "Disable Radio For Helicopters",
                exclusionsCfg.DisableRadioForHelicopters,
                "~y~Press the save button to apply changes."
            );
            NativeCheckboxItem disableRadioForTrains = GUI.CreateCheckboxItem(
                "Disable Radio For Trains",
                exclusionsCfg.DisableRadioForTrains,
                "~y~Press the save button to apply changes."
            );
            NativeCheckboxItem disableRadioForSubmarines = GUI.CreateCheckboxItem(
                "Disable Radio For Submarines",
                exclusionsCfg.DisableRadioForSubmarines,
                "~y~Press the save button to apply changes."
            );
            NativeCheckboxItem disableRadioForSubmarineCars = GUI.CreateCheckboxItem(
                "Disable Radio For Submarine Cars",
                exclusionsCfg.DisableRadioForSubmarineCars,
                "~y~Press the save button to apply changes."
            );
            NativeCheckboxItem disableRadioForRegularQuadBikes = GUI.CreateCheckboxItem(
                "Disable Radio For Regular Quad Bikes",
                exclusionsCfg.DisableRadioForRegularQuadBikes,
                "~y~Press the save button to apply changes."
            );
            NativeCheckboxItem disableRadioForMilitaryVehicles = GUI.CreateCheckboxItem(
                "Disable Radio For Military Vehicles",
                exclusionsCfg.DisableRadioForMilitaryVehicles,
                "~y~Press the save button to apply changes."
            );
            NativeMenu excludedVehiclesHashes = GUI.CreateAndRegisterMenu(
                UiMenuPool,
                "Excluded Vehicles",
                "Add or remove specific vehicle models"
            );

            List<VehicleHash> pendingVehicles = new List<VehicleHash>(exclusionsCfg.VehiclesWithNoRadio);
            List<NativeItem> vehicleItems = new List<NativeItem>();

            NativeItem addCurrentVehicleBtn = new NativeItem(
                "~g~Add Current Vehicle",
                "Adds the vehicle you are currently sitting in to the list to disable radio for it. ~y~Press the save changes button to apply."
            );

            addCurrentVehicleBtn.Activated += (s, e) =>
            {
                Ped player = Game.Player.Character;
                if (ValidFunctions.IsValidPed(player) && ValidFunctions.IsValidVehicle(player.CurrentVehicle))
                {
                    VehicleHash hash = (VehicleHash)player.CurrentVehicle.Model.Hash;

                    if (!pendingVehicles.Contains(hash))
                    {
                        pendingVehicles.Add(hash);
                        BuildVehicleList();
                        GUI.ShowContextualMessage($"Added {hash} to exclusions.");
                    }
                    else
                    {
                        GUI.ShowContextualMessage("Vehicle is already in the list.");
                    }
                }
                else
                {
                    GUI.ShowContextualMessage("You must be in a vehicle to add it.");
                }
            };

            void BuildVehicleList()
            {
                for (int i = 0; i < vehicleItems.Count; i++)
                {
                    excludedVehiclesHashes.Remove(vehicleItems[i]);
                }

                vehicleItems.Clear();

                for (int i = 0; i < pendingVehicles.Count; i++)
                {
                    VehicleHash hash = pendingVehicles[i];

                    NativeItem item = new NativeItem(
                        hash.ToString(),
                        "Click to remove from exclusion list. ~y~Press the save button to apply changes."
                    );
                    item.Activated += (s, e) =>
                    {
                        pendingVehicles.Remove(hash);
                        BuildVehicleList();
                        GUI.ShowContextualMessage($"Removed {hash}.");
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

            AddSaveAndResetButtons(
                _exclusionsSettingsMenu,
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

                    // Stage the default vehicle list; do NOT touch exclusionsCfg here.
                    pendingVehicles.Clear();
                    pendingVehicles.Add(VehicleHash.OpenWheel1);
                    pendingVehicles.Add(VehicleHash.OpenWheel2);
                    pendingVehicles.Add(VehicleHash.Formula);
                    pendingVehicles.Add(VehicleHash.Formula2);
                    BuildVehicleList();
                },
                saveAction: async () =>
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

                    // Commit the staged vehicle list to the live config.
                    exclusionsCfg.VehiclesWithNoRadio = pendingVehicles.ToArray();

                    await FileOperations.SaveSettingsSectionAsync(
                        exclusionsCfg,
                        cfg => cfg.RealisticVehicleRadio.Exclusions,
                        (cfg, settings) => cfg.RealisticVehicleRadio.Exclusions = settings
                    );

                    // NEW in V1.3.0: Tell the main script to wipe the cached exclusion booleans
                    _onExclusionsChanged?.Invoke();
                }
            );

            _exclusionsSettingsMenu.Opening += (s, e) =>
            {
                /* 
                   Fixed in V1.2.2
                   Only sync the checkboxes with the live config.
                   DO NOT clear/rebuild pendingVehicles here, or it will wipe out 
                   staged changes when the user navigates back from the submenu!
                   As the pendingVehicles list is already correctly initialized when
                   the script first loads, and it is correctly updated by the Add/Remove buttons.
                */

                disableRadioForMotorcycles.Checked = exclusionsCfg.DisableRadioForMotorcycles;
                disableRadioForPlanes.Checked = exclusionsCfg.DisableRadioForPlanes;
                disableRadioForAircraft.Checked = exclusionsCfg.DisableRadioForAircraft;
                disableRadioForQuadBikes.Checked = exclusionsCfg.DisableRadioForQuadBikes;
                disableRadioForHelicopters.Checked = exclusionsCfg.DisableRadioForHelicopters;
                disableRadioForTrains.Checked = exclusionsCfg.DisableRadioForTrains;
                disableRadioForSubmarines.Checked = exclusionsCfg.DisableRadioForSubmarines;
                disableRadioForSubmarineCars.Checked = exclusionsCfg.DisableRadioForSubmarineCars;
                disableRadioForRegularQuadBikes.Checked = exclusionsCfg.DisableRadioForRegularQuadBikes;
                disableRadioForMilitaryVehicles.Checked = exclusionsCfg.DisableRadioForMilitaryVehicles;

                if (_resetAllSettings)
                {
                    pendingVehicles.Clear();
                    pendingVehicles.AddRange(exclusionsCfg.VehiclesWithNoRadio);

                    BuildVehicleList();

                    _resetAllSettings = false;
                }
            };

            _realisticVehicleRadioSettingsMenu.AddSubMenu(_exclusionsSettingsMenu, "").Title = "Excluded Vehicles";
        }

        private void InitializeTunnelManagerMenu()
        {
            _manageTunnelsMenu = GUI.CreateAndRegisterMenu(
                UiMenuPool,
                "Manage Tunnels",
                "Select a tunnel to edit"
            );

            _editTunnelMenu = GUI.CreateAndRegisterMenu(
                UiMenuPool,
                "Edit Tunnel",
                "Delete, Rename, & Teleport to Recorded Tunnels"
            );

            NativeItem renameBtn = new NativeItem("~b~Rename");
            NativeItem teleportBtn = new NativeItem("~g~Teleport to Start");
            NativeItem deleteBtn = new NativeItem("~r~Delete");

            // --- RENAME LOGIC ---
            renameBtn.Activated += (s, e) =>
            {
                Tunnel tunnel = TunnelManager.Tunnels.Find(t => t.Id == _selectedTunnelId);
                if (tunnel == null)
                {
                    return;
                }

                // false allows spaces in the input
                string userInput = GUI.PromptUserForInput(false);
                if (userInput != null)
                {
                    // Update the manager and save to disk
                    TunnelManager.RenameTunnel(tunnel.Id, userInput);
                    tunnel.Name = userInput;

                    // Update UI instantly
                    _editTunnelMenu.BannerText.Text = $"{userInput}";
                }
            };

            // --- DELETE LOGIC ---
            deleteBtn.Activated += (s, e) =>
            {
                if (deleteBtn.Title != "~r~Click Again to Confirm")
                {
                    deleteBtn.Title = "~r~Click Again to Confirm";
                }
                else
                {
                    TunnelManager.DeleteTunnel(_selectedTunnelId);

                    // Reset button state for next time
                    deleteBtn.Title = "~r~Delete";

                    // Close edit menu (manageMenu will open via Closing event and refresh)
                    _editTunnelMenu.Visible = false;
                }
            };

            // --- TELEPORT LOGIC ---
            teleportBtn.Activated += (s, e) =>
            {
                Tunnel tunnel = TunnelManager.Tunnels.Find(t => t.Id == _selectedTunnelId);
                if (tunnel == null || tunnel.Points == null || tunnel.Points.Count == 0)
                {
                    GUI.ShowContextualMessage("~r~Tunnel has no recorded points.");
                    return;
                }

                Ped player = Game.Player.Character;
                if (!ValidFunctions.IsValidPed(player))
                {
                    return;
                }

                Vector3 targetPos = tunnel.Points[0].Center;
                float targetHeading = tunnel.Points[0].HeadingDeg;

                Vehicle currentVehicle = player.CurrentVehicle;
                if (ValidFunctions.IsValidVehicle(currentVehicle))
                {
                    currentVehicle.Position = targetPos;
                    currentVehicle.Heading = targetHeading - 90.0f; // Align car down the tunnel
                }
                else
                {
                    player.Position = targetPos;
                    player.Heading = targetHeading - 90.0f;
                }

                // Close menus so the player can immediately see the tunnel
                _editTunnelMenu.Visible = false;
                _manageTunnelsMenu.Visible = false;
            };

            _editTunnelMenu.Add(renameBtn);
            _editTunnelMenu.Add(teleportBtn);
            _editTunnelMenu.Add(deleteBtn);

            // When editMenu closes, go back to manageMenu and refresh its list
            _editTunnelMenu.Closing += (sender, args) =>
            {
                RefreshManageMenu();
                _manageTunnelsMenu.Visible = true;

                // Restore it to the default value in case a user pressed it once then closed the menu
                if (deleteBtn.Title != "~r~Delete")
                {
                    deleteBtn.Title = "~r~Delete";
                }
            };

            _editTunnelMenu.SelectedIndexChanged += (sender, args) =>
            {
                if (deleteBtn.Title != "~r~Delete")
                {
                    deleteBtn.Title = "~r~Delete";
                }
            };

            // Call this whenever the menu is opened to refresh the list
            _manageTunnelsMenu.Shown += (sender, args) =>
            {
                RefreshManageMenu();
            };

            MainMenu.AddSubMenu(_manageTunnelsMenu, "").Title = "Manage Tunnels";
        }

        private void RefreshManageMenu()
        {
            _manageTunnelsMenu.Clear();

            if (TunnelManager.Tunnels.Count == 0)
            {
                _manageTunnelsMenu.Add(
                    new NativeItem(
                        "No tunnels recorded",
                        "Use the Tunnel Recorder to create one."
                    )
                );
                return;
            }

            for (int i = 0; i < TunnelManager.Tunnels.Count; i++)
            {
                Tunnel tunnel = TunnelManager.Tunnels[i];
                Tunnel capturedTunnel = tunnel; // Capture locally for the lambda

                NativeItem item = new NativeItem(
                    $"{capturedTunnel.Name}",
                    $"ID: #{capturedTunnel.Id} | Points: {capturedTunnel.Points.Count}"
                );

                item.Activated += (s, e) =>
                {
                    _selectedTunnelId = capturedTunnel.Id;

                    // Update edit menu UI to reflect selected tunnel
                    _editTunnelMenu.BannerText.Text = $"{capturedTunnel.Name}";

                    _manageTunnelsMenu.Visible = false;
                    _editTunnelMenu.Visible = true;
                };

                _manageTunnelsMenu.Add(item);
            }
        }

        public void RefreshTunnelManagerMenuIfVisible()
        {
            if (_manageTunnelsMenu == null || _editTunnelMenu == null)
            {
                return;
            }

            // Refresh if the tunnel manager UI is currently active
            if (_manageTunnelsMenu.Visible || _editTunnelMenu.Visible)
            {
                RefreshManageMenu();
            }
        }

        private void AddResetAllSettingsToDefaultBtn(Configuration cfg)
        {
            NativeItem resetAllSettingsBtn = new NativeItem(
                    "~r~Reset All Settings to Default",
                    "Resets every setting in every menu to its default value and saves to disk. This won't delete the recorded tunnels. ~y~However, this will reset excluded vehicles list. ~r~This cannot be undone."
                );

            resetAllSettingsBtn.Activated += async (s, e) =>
            {
                if (resetAllSettingsBtn.Title != "~r~Click Again to Confirm Reset")
                {
                    resetAllSettingsBtn.Title = "~r~Click Again to Confirm Reset";
                    return;
                }

                ResetButtonTitle();

                try
                {
                    cfg.ResetToDefaults();

                    TunnelRecorder.PendingTunnelName = "New Tunnel";

                    _resetAllSettings = true;

                    await FileOperations.SaveConfigurationAsync(cfg);

                    TunnelSpatialIndex.MarkDirty();
                    _onExclusionsChanged?.Invoke();

                    GUI.ShowContextualMessage("~g~All settings have been reset to defaults.");
                }
                catch (Exception ex)
                {
                    Logger.DisplayAndLogError(ex, "Failed to reset settings to defaults.");
                }
            };

            void ResetButtonTitle()
            {
                if (resetAllSettingsBtn.Title != "~r~Reset All Settings to Default")
                {
                    resetAllSettingsBtn.Title = "~r~Reset All Settings to Default";
                }
            }

            _mainSettingsTreeMenu.Closing += (s, e) => ResetButtonTitle();
            _mainSettingsTreeMenu.SelectedIndexChanged += (s, e) => ResetButtonTitle();

            _mainSettingsTreeMenu.Add(resetAllSettingsBtn);
        }

        private void InitializeThunderstormSettingsMenu(Configuration.ThunderstormSettings thunderCfg)
        {
            _thunderstormSettingsMenu = GUI.CreateAndRegisterMenu(
                UiMenuPool,
                "Thunderstorm Interference",
                "Brief radio interference and static during thunderstorms"
            );

            // --- Mute Bursts Pending Variables ---
            float pendingInitialMin = thunderCfg.InitialInterferenceDelayMin;
            float pendingInitialMax = thunderCfg.InitialInterferenceDelayMax;
            float pendingDurationMin = thunderCfg.InterferenceDurationMin;
            float pendingDurationMax = thunderCfg.InterferenceDurationMax;
            float pendingNextMin = thunderCfg.NextInterferenceDelayMin;
            float pendingNextMax = thunderCfg.NextInterferenceDelayMax;

            // --- Background Static Pending Variables ---
            bool pendingEnableStatic = thunderCfg.EnableThunderstormStatic;
            bool pendingEnableStaticPop = thunderCfg.EnableStaticPop;
            float pendingStaticMin = thunderCfg.StaticSignalMin;
            float pendingStaticMax = thunderCfg.StaticSignalMax;
            float pendingStaticDriftSpeed = thunderCfg.StaticDriftSpeed;
            float pendingStaticHissIntensity = thunderCfg.StaticHissIntensity;
            int pendingStaticPopChancePercentage = thunderCfg.StaticPopChancePercentage;
            float pendingStaticPopMaxIntensity = thunderCfg.StaticPopMaxIntensity;

            // ==========================================
            // 1. MUTE BURSTS UI
            // ==========================================
            NativeCheckboxItem enableThunderInterference = GUI.CreateCheckboxItem(
                "Enable Thunderstorm Interference",
                thunderCfg.EnableThunderstormInterference,
                "When enabled, vehicle radios briefly cut out during THUNDER weather. ~y~Press the save button to apply changes."
            );
            NativeItem initialDelayMin = new NativeItem(
                $"Initial Delay Min ({pendingInitialMin} s)",
                "Minimum delay after a thunderstorm starts before the first interference event. ~y~Must be smaller than Initial Delay Max. Press the save button to apply changes."
            );
            NativeItem initialDelayMax = new NativeItem(
                $"Initial Delay Max ({pendingInitialMax} s)",
                "Maximum delay after a thunderstorm starts before the first interference event. ~y~Must be larger than Initial Delay Min. Press the save button to apply changes."
            );
            NativeItem durationMin = new NativeItem(
                $"Interference Duration Min ({pendingDurationMin} s)",
                "Minimum duration of each radio interference event. ~y~Must be smaller than Interference Duration Max. Press the save button to apply changes."
            );
            NativeItem durationMax = new NativeItem(
                $"Interference Duration Max ({pendingDurationMax} s)",
                "Maximum duration of each radio interference event. ~y~Must be larger than Interference Duration Min. Press the save button to apply changes."
            );
            NativeItem nextDelayMin = new NativeItem(
                $"Next Delay Min ({pendingNextMin} s)",
                "Minimum delay between interference events. ~y~Must be smaller than Next Delay Max. Press the save button to apply changes."
            );
            NativeItem nextDelayMax = new NativeItem(
                $"Next Delay Max ({pendingNextMax} s)",
                "Maximum delay between interference events. ~y~Must be larger than Next Delay Min. Press the save button to apply changes."
            );

            // ==========================================
            // 2. BACKGROUND STATIC UI
            // ==========================================
            NativeCheckboxItem enableThunderStatic = GUI.CreateCheckboxItem(
                "Enable Background Static",
                thunderCfg.EnableThunderstormStatic,
                "When enabled, the radio signal degrades with organic static while the THUNDER weather is active. ~y~Press the save button to apply changes."
            );

            NativeItem staticSignalMin = new NativeItem(
                $"Static Min Signal ({pendingStaticMin})",
                "The heaviest the static gets during the storm. ~y~Lower = heavier static. Must be smaller than Static Max Signal. Press the save button to apply changes."
            );
            NativeItem staticSignalMax = new NativeItem(
                $"Static Max Signal ({pendingStaticMax})",
                "The lightest the static gets during the storm. ~y~Higher = clearer signal. Must be larger than Static Min Signal. Press the save button to apply changes."
            );
            NativeItem staticDriftSpeed = new NativeItem(
                $"Static Drift Speed ({pendingStaticDriftSpeed})",
                "Speed of the organic Perlin noise drift. ~y~Higher = faster atmospheric changes. Press the save button to apply changes."
            );
            NativeItem staticHissIntensity = new NativeItem(
                $"Static Hiss Intensity ({pendingStaticHissIntensity})",
                "Intensity of the high-frequency Perlin hiss. ~y~Higher = more white noise texture. Press the save button to apply changes."
            );

            NativeCheckboxItem enableStaticPop = GUI.CreateCheckboxItem(
                "Enable Static Pop",
                thunderCfg.EnableStaticPop,
                "When enabled, the radio signal may experience a sharp static pop for a brief moment. ~y~Press the save button to apply changes."
            );

            NativeItem staticPopChance = new NativeItem(
                $"Static Pop Chance ({pendingStaticPopChancePercentage} %)",
                "The probability of a static pop occurring per second, as a percentage. ~y~Higher = more static pops. Press the save button to apply changes."
            );
            NativeItem staticPopMaxIntensity = new NativeItem(
                $"Static Pop Max Intensity ({pendingStaticPopMaxIntensity})",
                "Maximum signal deduction when a random pop occurs. ~y~Higher = stronger effect. Press the save button to apply changes."
            );

            // ==========================================
            // BIND INPUTS
            // ==========================================
            BindFloatInput(
                initialDelayMin,
                val =>
                {
                    pendingInitialMin = val;
                    if (pendingInitialMin > pendingInitialMax)
                    {
                        pendingInitialMax = pendingInitialMin;
                        initialDelayMax.Title = $"Initial Delay Max ({pendingInitialMax} s)";
                    }
                },
                val => Math.Max(0f, val),
                "Initial Delay Min ({0} s)",
                "Enter a value of 0 or greater."
            );
            BindFloatInput(
                initialDelayMax,
                val =>
                {
                    pendingInitialMax = val;
                    if (pendingInitialMax < pendingInitialMin)
                    {
                        pendingInitialMin = pendingInitialMax;
                        initialDelayMin.Title = $"Initial Delay Min ({pendingInitialMin} s)";
                    }
                },
                val => Math.Max(0f, val),
                "Initial Delay Max ({0} s)",
                "Enter a value of 0 or greater."
            );
            BindFloatInput(
                durationMin,
                val =>
                {
                    pendingDurationMin = val;
                    if (pendingDurationMin > pendingDurationMax)
                    {
                        pendingDurationMax = pendingDurationMin;
                        durationMax.Title = $"Interference Duration Max ({pendingDurationMax} s)";
                    }
                },
                val => Math.Max(0f, val),
                "Interference Duration Min ({0} s)",
                "Enter a value of 0 or greater."
            );
            BindFloatInput(
                durationMax,
                val =>
                {
                    pendingDurationMax = val;
                    if (pendingDurationMax < pendingDurationMin)
                    {
                        pendingDurationMin = pendingDurationMax;
                        durationMin.Title = $"Interference Duration Min ({pendingDurationMin} s)";
                    }
                },
                val => Math.Max(0f, val),
                "Interference Duration Max ({0} s)",
                "Enter a value of 0 or greater."
            );
            BindFloatInput(
                nextDelayMin,
                val =>
                {
                    pendingNextMin = val;
                    if (pendingNextMin > pendingNextMax)
                    {
                        pendingNextMax = pendingNextMin;
                        nextDelayMax.Title = $"Next Delay Max ({pendingNextMax} s)";
                    }
                },
                val => Math.Max(0f, val),
                "Next Delay Min ({0} s)",
                "Enter a value of 0 or greater."
            );
            BindFloatInput(
                nextDelayMax,
                val =>
                {
                    pendingNextMax = val;
                    if (pendingNextMax < pendingNextMin)
                    {
                        pendingNextMin = pendingNextMax;
                        nextDelayMin.Title = $"Next Delay Min ({pendingNextMin} s)";
                    }
                },
                val => Math.Max(0f, val),
                "Next Delay Max ({0} s)",
                "Enter a value of 0 or greater."
            );

            BindFloatInput(
                staticSignalMin,
                val =>
                {
                    pendingStaticMin = val;
                    if (pendingStaticMin > pendingStaticMax)
                    {
                        pendingStaticMax = pendingStaticMin;
                        staticSignalMax.Title = $"Static Max Signal ({pendingStaticMax})";
                    }
                },
                val => MathFunctions.Clamp(val, 0.0f, 1.0f),
                "Static Min Signal ({0})",
                "Enter a value between 0.0 and 1.0."
            );
            BindFloatInput(
                staticSignalMax,
                val =>
                {
                    pendingStaticMax = val;
                    if (pendingStaticMax < pendingStaticMin)
                    {
                        pendingStaticMin = pendingStaticMax;
                        staticSignalMin.Title = $"Static Min Signal ({pendingStaticMin})";
                    }
                },
                val => MathFunctions.Clamp(val, 0.0f, 1.0f),
                "Static Max Signal ({0})",
                "Enter a value between 0.0 and 1.0."
            );
            BindFloatInput(
                staticDriftSpeed,
                val => pendingStaticDriftSpeed = val,
                val => Math.Max(0f, val),
                "Static Drift Speed ({0})",
                "Enter a value of 0 or greater."
            );
            BindFloatInput(
                staticHissIntensity,
                val => pendingStaticHissIntensity = val,
                val => Math.Max(0f, val),
                "Static Hiss Intensity ({0})",
                "Enter a value of 0 or greater."
            );
            BindIntInput(
                staticPopChance,
                val => pendingStaticPopChancePercentage = val,
                val => MathFunctions.Clamp(val, 0, 100),
                "Static Pop Chance ({0} %)",
                "Enter a percentage between 0 and 100."
            );
            BindFloatInput(
                staticPopMaxIntensity,
                val => pendingStaticPopMaxIntensity = val,
                val => MathFunctions.Clamp(val, 0.0f, 1.0f),
                "Static Pop Max Intensity ({0})",
                "Enter a value between 0.0 and 1.0."
            );

            // ==========================================
            // ADD TO MENU
            // ==========================================
            _thunderstormSettingsMenu.Add(enableThunderInterference);
            _thunderstormSettingsMenu.Add(initialDelayMin);
            _thunderstormSettingsMenu.Add(initialDelayMax);
            _thunderstormSettingsMenu.Add(durationMin);
            _thunderstormSettingsMenu.Add(durationMax);
            _thunderstormSettingsMenu.Add(nextDelayMin);
            _thunderstormSettingsMenu.Add(nextDelayMax);

            _thunderstormSettingsMenu.Add(enableThunderStatic);
            _thunderstormSettingsMenu.Add(staticSignalMin);
            _thunderstormSettingsMenu.Add(staticSignalMax);
            _thunderstormSettingsMenu.Add(staticDriftSpeed);
            _thunderstormSettingsMenu.Add(staticHissIntensity);
            _thunderstormSettingsMenu.Add(enableStaticPop);
            _thunderstormSettingsMenu.Add(staticPopChance);
            _thunderstormSettingsMenu.Add(staticPopMaxIntensity);

            // ==========================================
            // SAVE & RESET
            // ==========================================
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

                    enableThunderStatic.Checked = true;
                    pendingStaticMin = 0.4f;
                    pendingStaticMax = 0.8f;
                    pendingStaticDriftSpeed = 0.6f;
                    pendingStaticHissIntensity = 0.15f;
                    enableStaticPop.Checked = true;
                    pendingStaticPopChancePercentage = 8;
                    pendingStaticPopMaxIntensity = 0.35f;

                    initialDelayMin.Title = $"Initial Delay Min ({pendingInitialMin} s)";
                    initialDelayMax.Title = $"Initial Delay Max ({pendingInitialMax} s)";
                    durationMin.Title = $"Interference Duration Min ({pendingDurationMin} s)";
                    durationMax.Title = $"Interference Duration Max ({pendingDurationMax} s)";
                    nextDelayMin.Title = $"Next Delay Min ({pendingNextMin} s)";
                    nextDelayMax.Title = $"Next Delay Max ({pendingNextMax} s)";

                    staticSignalMin.Title = $"Static Min Signal ({pendingStaticMin})";
                    staticSignalMax.Title = $"Static Max Signal ({pendingStaticMax})";
                    staticDriftSpeed.Title = $"Static Drift Speed ({pendingStaticDriftSpeed})";
                    staticHissIntensity.Title = $"Static Hiss Intensity ({pendingStaticHissIntensity})";
                    staticPopChance.Title = $"Static Pop Chance ({pendingStaticPopChancePercentage} %)";
                    staticPopMaxIntensity.Title = $"Static Pop Max Intensity ({pendingStaticPopMaxIntensity})";
                },
                saveAction: async () =>
                {
                    thunderCfg.EnableThunderstormInterference = enableThunderInterference.Checked;
                    thunderCfg.InitialInterferenceDelayMin = pendingInitialMin;
                    thunderCfg.InitialInterferenceDelayMax = pendingInitialMax;
                    thunderCfg.InterferenceDurationMin = pendingDurationMin;
                    thunderCfg.InterferenceDurationMax = pendingDurationMax;
                    thunderCfg.NextInterferenceDelayMin = pendingNextMin;
                    thunderCfg.NextInterferenceDelayMax = pendingNextMax;

                    thunderCfg.EnableThunderstormStatic = enableThunderStatic.Checked;
                    thunderCfg.StaticSignalMin = pendingStaticMin;
                    thunderCfg.StaticSignalMax = pendingStaticMax;
                    thunderCfg.StaticDriftSpeed = pendingStaticDriftSpeed;
                    thunderCfg.StaticHissIntensity = pendingStaticHissIntensity;
                    thunderCfg.EnableStaticPop = enableStaticPop.Checked;
                    thunderCfg.StaticPopChancePercentage = pendingStaticPopChancePercentage;
                    thunderCfg.StaticPopMaxIntensity = pendingStaticPopMaxIntensity;

                    // Ensure Min never exceeds Max before persisting to disk
                    thunderCfg.NormalizeMinMaxPairs();

                    await FileOperations.SaveSettingsSectionAsync(
                        thunderCfg,
                        cfg => cfg.RealisticVehicleRadio.Thunderstorm,
                        (cfg, settings) => cfg.RealisticVehicleRadio.Thunderstorm = settings
                    );
                }
            );

            _thunderstormSettingsMenu.Opening += (s, e) =>
            {
                enableThunderInterference.Checked = thunderCfg.EnableThunderstormInterference;
                pendingInitialMin = thunderCfg.InitialInterferenceDelayMin;
                initialDelayMin.Title = $"Initial Delay Min ({pendingInitialMin} s)";
                pendingInitialMax = thunderCfg.InitialInterferenceDelayMax;
                initialDelayMax.Title = $"Initial Delay Max ({pendingInitialMax} s)";
                pendingDurationMin = thunderCfg.InterferenceDurationMin;
                durationMin.Title = $"Interference Duration Min ({pendingDurationMin} s)";
                pendingDurationMax = thunderCfg.InterferenceDurationMax;
                durationMax.Title = $"Interference Duration Max ({pendingDurationMax} s)";
                pendingNextMin = thunderCfg.NextInterferenceDelayMin;
                nextDelayMin.Title = $"Next Delay Min ({pendingNextMin} s)";
                pendingNextMax = thunderCfg.NextInterferenceDelayMax;
                nextDelayMax.Title = $"Next Delay Max ({pendingNextMax} s)";

                enableThunderStatic.Checked = thunderCfg.EnableThunderstormStatic;
                pendingStaticMin = thunderCfg.StaticSignalMin;
                staticSignalMin.Title = $"Static Min Signal ({pendingStaticMin})";
                pendingStaticMax = thunderCfg.StaticSignalMax;
                staticSignalMax.Title = $"Static Max Signal ({pendingStaticMax})";
                pendingStaticDriftSpeed = thunderCfg.StaticDriftSpeed;
                staticDriftSpeed.Title = $"Static Drift Speed ({pendingStaticDriftSpeed})";
                pendingStaticHissIntensity = thunderCfg.StaticHissIntensity;
                staticHissIntensity.Title = $"Static Hiss Intensity ({pendingStaticHissIntensity})";

                enableStaticPop.Checked = thunderCfg.EnableStaticPop;
                pendingStaticPopChancePercentage = thunderCfg.StaticPopChancePercentage;
                staticPopChance.Title = $"Static Pop Chance ({pendingStaticPopChancePercentage} %)";
                pendingStaticPopMaxIntensity = thunderCfg.StaticPopMaxIntensity;
                staticPopMaxIntensity.Title = $"Static Pop Max Intensity ({pendingStaticPopMaxIntensity})";
            };

            _realisticVehicleRadioSettingsMenu.AddSubMenu(_thunderstormSettingsMenu, "").Title = "Thunderstorm Interference";
        }

        // ==========================================
        // HELPER METHODS TO REDUCE REPETITION
        // ==========================================
        // Improved in V1.3.0: Removed the validator since the configuration class handles the valid ranges
        private void BindFloatInput(NativeItem item, Action<float> setter, Func<float, float> clamp, string titleTemplate, string errorMsg)
        {
            item.Activated += (s, e) =>
            {
                string userInput = GUI.PromptUserForInput(true);
                if (float.TryParse(userInput, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                {
                    value = clamp(value);
                    setter(value);
                    item.Title = string.Format(CultureInfo.InvariantCulture, titleTemplate, value);
                }
                else
                {
                    string settingName = item.Title.Contains("(") ? item.Title.Split('(')[0].Trim() : item.Title;
                    GUI.ShowContextualMessage($"~w~Invalid ~r~{settingName}~w~ input. {errorMsg}");
                }
            };
        }
        private void BindIntInput(NativeItem item, Action<int> setter, Func<int, int> clamp, string titleTemplate, string errorMsg)
        {
            item.Activated += (s, e) =>
            {
                string userInput = GUI.PromptUserForInput(true);
                if (int.TryParse(userInput, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                {
                    value = clamp(value);
                    setter(value);
                    item.Title = string.Format(CultureInfo.InvariantCulture, titleTemplate, value);
                }
                else
                {
                    string settingName = item.Title.Contains("(") ? item.Title.Split('(')[0].Trim() : item.Title;
                    GUI.ShowContextualMessage($"~w~Invalid ~r~{settingName}~w~ input. {errorMsg}");
                }
            };
        }

        private void AddSaveAndResetButtons(NativeMenu menu, Action resetAction, Func<Task> saveAction)
        {
            NativeItem restToDefaultBtn = new NativeItem(
                "~b~Reset to Defaults",
                "Resets all options within this menu to default. " +
                "~y~Press the save button to apply changes."
            );

            restToDefaultBtn.Activated += (s, i) => resetAction();

            NativeItem saveBtn = new NativeItem("~g~Save Changes");
            saveBtn.Activated += async (s, i) =>
            {
                try
                {
                    await saveAction(); // Await the task
                }
                catch (Exception ex)
                {
                    Logger.DisplayAndLogError(ex, "Menu save action failed.");
                }
            };

            menu.Add(restToDefaultBtn);
            menu.Add(saveBtn);
        }
        private void BindKeyAssignment(NativeMenu menu, NativeListItem<string> menuItem, string displayName)
        {
            menuItem.Activated += (sender, item) =>
            {
                KeybindOverlay.Start($"~y~KEY BINDING~w~~n~Press a key to assign~n~~b~{displayName}");

                WaitingForKeyAssignment = true;
                PendingListItem = menuItem;
                PendingMenu = menu;
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetKeyIndex(string key)
        {
            Enum.TryParse(key, true, out Keys parsedKey);
            return Array.IndexOf(KeysNames, parsedKey.ToString());
        }
    }
}