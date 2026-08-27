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
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Collections.Generic;

using static Menus;
using static Helpers;

using GTA;
using GTA.Math;
using GTA.Native;
using Input = GTA.Input;  // Does not exist in SHVDNE
using Screen = GTA.UI.Screen;

public sealed class RealisticVehicleRadio : Script
{
    private const int _ACTIVE_INTERVAL = 1;
    private const int _DEFAULT_INTERVAL = 100;

    // Throttle heavy logic to 100ms
    private const int _LOGIC_INTERVAL_MS = 100;
    private int _nextLogicTime = 0;

    // Cache vehicles for smooth debug drawing
    private Vehicle[] _cachedVehicles = Array.Empty<Vehicle>();
    private int _cachedProcessLimit = 0;

    // per-vehicle state
    private readonly Dictionary<int, VehicleRadioState> _vehicleStates = new Dictionary<int, VehicleRadioState>();
    private readonly Dictionary<int, bool> _vehicleRadioEnabledStates = new Dictionary<int, bool>();

    // Persistent tracking for cleanup on script abort/reload
    private readonly HashSet<int> _touchedRadioEnabled = new HashSet<int>();
    private readonly HashSet<int> _touchedLoud = new HashSet<int>();

    // ==========================================
    // CACHED CONFIGURATION INSTANCES
    // ==========================================
    private readonly Configuration _config;
    private readonly Configuration.MainSettings _mainCfg;
    private readonly Configuration.LoudnessSettings _loudnessCfg;
    private readonly Configuration.TunnelDetectionSettings _tunnelCfg;
    private readonly Configuration.VehicleExclusions _exclusionsCfg;
    private readonly Configuration.TunnelDebugVisualizerSettings _debugCfg;
    private readonly Configuration.TunnelRecorderSettings _recorderCfg;
    private readonly Configuration.ThunderstormSettings _thunderCfg;

    private readonly Menus _menus;

    private enum ThunderInterferenceState
    {
        Inactive,
        Waiting,
        Interfering
    }

    private const int WEATHER_THUNDER_HASH = -1233681761;

    private ThunderInterferenceState _thunderState = ThunderInterferenceState.Inactive;
    private bool _thunderInterferenceActive = false;
    private int _nextThunderTransitionTime = 0;

    // per-vehicle persisted state
    private class VehicleRadioState
    {
        public VehicleRoofState PrevRoofState;
        public bool PrevAnyWindowBroken;
        public int PrevOpenMask;
        public int PrevBrokenMask;

        // decision cache
        public bool RandomChecked;
        public bool RandomResult;

        // whether we already applied loud to avoid duplicate native calls
        public bool LoudApplied;

        // NEW in V1.2.0: remember which features were enabled when the last decision was made
        public bool LastRoofEnabled;
        public bool LastWindowEnabled;
        public bool LastDoorEnabled;
        public bool LastRandomEnabled;
    }

    public struct TunnelPoint
    {
        public Vector3 Center;
        public Vector3 LeftWall;
        public Vector3 RightWall;
        public float FloorZ;
        public float CeilingZ;
        public float Width;
        public float HeadingDeg;

        public TunnelPoint(Vector3 center, Vector3 leftWall, Vector3 rightWall, float floorZ, float ceilingZ, float width, float headingDeg)
        {
            Center = center;
            LeftWall = leftWall;
            RightWall = rightWall;
            FloorZ = floorZ;
            CeilingZ = ceilingZ;
            Width = width;
            HeadingDeg = headingDeg;
        }
    }

    public RealisticVehicleRadio()
    {
        MathFunctions.Seed((uint)DateTime.UtcNow.Ticks);

        // 1. Load Configuration from JSON
        _config = FileOperations.LoadConfiguration();
        TunnelManager.Load();

        // 2. Cache nested classes for high-performance access in OnTick
        _mainCfg = _config.RealisticVehicleRadio.Main;
        _loudnessCfg = _config.RealisticVehicleRadio.Loudness;
        _tunnelCfg = _config.RealisticVehicleRadio.TunnelDetection;
        _exclusionsCfg = _config.RealisticVehicleRadio.Exclusions;
        _debugCfg = _config.TunnelDebugVisualizer;
        _recorderCfg = _config.TunnelRecorder;
        _thunderCfg = _config.RealisticVehicleRadio.Thunderstorm;

        TunnelSpatialIndex.EnsureBuilt(
            TunnelManager.Tunnels,
            _tunnelCfg.TunnelHorizontalTolerance,
            _tunnelCfg.TunnelVerticalTolerance
        );

        _menus = new Menus(_mainCfg, _loudnessCfg, _tunnelCfg, _exclusionsCfg, _debugCfg, _recorderCfg, _thunderCfg);

        if (_mainCfg.AutoCheckForUpdatesOnStartup)
        {
            Task.Run(async () => await UpdateChecker.CheckForUpdatesAsync());
        }

        Tick += OnTick;
        KeyDown += OnKeyDown;
        Aborted += OnAbort;
        Interval = _DEFAULT_INTERVAL;
    }

    private void OnAbort(object sender, EventArgs e)
    {
        RestoreVehicleRadioStates();
        TunnelSaveQueue.FlushBlocking();
    }

    private void OnTick(object sender, EventArgs e)
    {
        Interval = TunnelDebugVisualizer.EnableDebug ||
                TunnelRecorder.EnableTunnelRecorder ||
                IsManualRecordingActive ||
                UiMenuPool.AreAnyVisible ||
                WaitingForKeyAssignment ||
                Alpha > 0 ? _ACTIVE_INTERVAL : _DEFAULT_INTERVAL;

        TunnelSaveQueue.Process();

        // ==========================================
        // 1. UI & RENDERING (Runs EVERY FRAME)
        // ==========================================
        UiMenuPool.Process();

        if (WaitingForKeyAssignment)
        {
            // Disable
            Input.Controls.DisableAllControlActionsThisFrame(Input.ControlType.PlayerControl);
            // Use Game.DisableAllControlsThisFrame(); while using SHVDNE

            // Hide the specific menu that triggered the keybind
            if (PendingMenu != null)
            {
                PendingMenu.Visible = false;
            }

            for (int i = 1; i <= 22; i++)
            {
                Function.Call(Hash.HIDE_HUD_COMPONENT_THIS_FRAME, i);
            }
            Function.Call(Hash.DISABLE_FRONTEND_THIS_FRAME);
            Function.Call(Hash.HIDE_HUD_AND_RADAR_THIS_FRAME);

            // Draw
            Alpha += (int)(150f * Game.LastFrameTime);
            Alpha = Math.Min(Alpha, 170);
            DrawFadeOverlay(GetTypingText());
        }
        else if (Alpha > 0)
        {
            Alpha -= (int)(150f * Game.LastFrameTime);
            Alpha = Math.Max(Alpha, 0);

            DrawFadeOverlay(TypedOverlayText);
        }

        Ped playerPed = Game.Player.Character;
        bool isValidPlayer = ValidFunctions.IsValidPed(playerPed);
        Vector3 playerPosition = isValidPlayer ? playerPed.Position : Vector3.Zero;

        UpdateThunderstormInterference();

        if (isValidPlayer)
        {
            if (TunnelDebugVisualizer.EnableDebug)
            {
                // Draw all saved tunnels individually to prevent cross-map lines
                TunnelDebugVisualizer.DrawAllTunnels(playerPosition, TunnelManager.Tunnels, _debugCfg);

                // Smoothly draw vehicle projections
                Vehicle currentPlayerVehicle = playerPed.CurrentVehicle;
                if (ValidFunctions.IsValidVehicle(currentPlayerVehicle))
                {
                    TunnelDebugVisualizer.DrawVehicleProjectionDebug(currentPlayerVehicle.Position, TunnelManager.Tunnels);
                }

                // If actively recording, ALSO draw the LIVE samples (Magenta/Orange)
                if (TunnelRecorder.EnableTunnelRecorder)
                {
                    TunnelPoint[] liveRecording = TunnelRecorder.GetLiveRecording();
                    if (liveRecording != null && liveRecording.Length > 0)
                    {
                        TunnelDebugVisualizer.DrawCapturedTunnelDebug(playerPosition, liveRecording, _debugCfg, isLiveRecording: true);
                    }
                }
            }

            if (IsManualRecordingActive && TunnelDebugVisualizer.EnableDebug)
            {
                Vehicle currentPlayerVehicle = playerPed.CurrentVehicle;
                if (ValidFunctions.IsValidVehicle(currentPlayerVehicle))
                {
                    TunnelPoint? previewPoint = TunnelRecorder.ComputePreviewPoint(currentPlayerVehicle, _recorderCfg);
                    if (previewPoint.HasValue)
                    {
                        TunnelDebugVisualizer.DrawSinglePointPreview(previewPoint, playerPosition, _debugCfg);
                    }
                }
                else
                {
                    Screen.ShowHelpText("~b~Manual Recording:~b~ ~o~Enter a vehicle to see preview.", 100, true, false);
                }
            }

            if (TunnelRecorder.EnableTunnelRecorder)
            {
                Vehicle currentPlayerVehicle = playerPed.CurrentVehicle;

                if (!ValidFunctions.IsValidVehicle(currentPlayerVehicle))
                {
                    Screen.ShowHelpText("~b~Tunnel Recorder:~b~ ~o~Enter a vehicle to start recording", 2000, false);

                    // Safely turn it off and clear internal state if they aren't in a car
                    TunnelRecorder.EnableTunnelRecorder = false;
                    TunnelRecorder.HandleToggleKey();
                }
                else
                {
                    TunnelRecorder.Record(currentPlayerVehicle, playerPosition, _recorderCfg);
                }
            }
        }

        // ==========================================
        // 2. HEAVY LOGIC (Throttled to 100ms)
        // ==========================================
        int now = Game.GameTime;

        if (now >= _nextLogicTime)
        {
            if (_mainCfg.DisableRadioInTunnels || TunnelDebugVisualizer.EnableDebug)
            {
                TunnelSpatialIndex.EnsureBuilt(
                    TunnelManager.Tunnels,
                    _tunnelCfg.TunnelHorizontalTolerance,
                    _tunnelCfg.TunnelVerticalTolerance
                );
            }

            _nextLogicTime = now + _LOGIC_INTERVAL_MS;

            // Use distance-based vehicle search; set VehiclesSearchDistance <= 0 to include all vehicles
            _cachedVehicles = playerPosition != Vector3.Zero && _mainCfg.VehiclesSearchDistance > 0
                ? World.GetNearbyVehicles(playerPosition, _mainCfg.VehiclesSearchDistance)
                : World.GetAllVehicles();

            if (_cachedVehicles?.Length == 0)
            {
                // clear stale states if no vehicles
                _cachedProcessLimit = 0;
                _vehicleStates.Clear();
                _vehicleRadioEnabledStates.Clear();
                return;
            }

            // Determine how many vehicles to process based on the limit setting
            _cachedProcessLimit = _mainCfg.MaxProcessVehicles > 0 ? Math.Min(_cachedVehicles.Length, _mainCfg.MaxProcessVehicles) : _cachedVehicles.Length;
            HashSet<int> activeHandles = new HashSet<int>();

            for (int i = 0; i < _cachedProcessLimit; i++)
            {
                Vehicle vehicle = _cachedVehicles[i];
                if (!ValidFunctions.IsValidVehicle(vehicle))
                {
                    continue;
                }

                // Radio is off already by default when engine is not running, so no need to procced further
                if (!vehicle.IsEngineRunning)
                {
                    continue;
                }

                int handle = vehicle.Handle;
                activeHandles.Add(handle);
                bool isVehicleWithNoRadio = false;
                for (int x = 0; x < _exclusionsCfg.VehiclesWithNoRadio.Length; x++)
                {
                    if (_exclusionsCfg.VehiclesWithNoRadio[x] == (VehicleHash)vehicle.Model.Hash)
                    {
                        isVehicleWithNoRadio = true;
                        break;
                    }
                }

                // exclude certain vehicle classes
                if ((_exclusionsCfg.DisableRadioForMotorcycles && vehicle.IsMotorcycle) ||
                    (_exclusionsCfg.DisableRadioForPlanes && vehicle.IsPlane) ||
                    (_exclusionsCfg.DisableRadioForAircraft && vehicle.IsAircraft) ||
                    (_exclusionsCfg.DisableRadioForQuadBikes && vehicle.IsQuadBike) ||
                    (_exclusionsCfg.DisableRadioForHelicopters && vehicle.IsHelicopter) ||
                    (_exclusionsCfg.DisableRadioForTrains && vehicle.IsTrain) ||
                    (_exclusionsCfg.DisableRadioForSubmarines && vehicle.IsSubmarine) ||
                    (_exclusionsCfg.DisableRadioForSubmarineCars && vehicle.IsSubmarineCar) ||
                    (_exclusionsCfg.DisableRadioForRegularQuadBikes && vehicle.IsRegularQuadBike) ||
                    (_exclusionsCfg.DisableRadioForMilitaryVehicles && vehicle.ClassType == VehicleClass.Military) ||
                    isVehicleWithNoRadio)
                {
                    // Only call natives when the state actually changes
                    if (!_vehicleRadioEnabledStates.TryGetValue(handle, out bool lastEnabled) || lastEnabled)
                    {
                        Function.Call(Hash.SET_VEHICLE_RADIO_ENABLED, handle, false);
                        Function.Call(Hash.SET_VEH_RADIO_STATION, handle, "OFF");

                        _touchedRadioEnabled.Add(handle);

                        /*
                        Important:         
                        Remember that this vehicle is currently disabled
                        This allows the script to re-enable it later if it is no longer excluded
                        */
                        _vehicleRadioEnabledStates[handle] = false;
                    }

                    // ensure we don't keep state for excluded vehicles
                    _vehicleStates.Remove(handle);

                    continue;
                }

                // ==========================================
                // 1. LOUDNESS FEATURES (Roof, Windows, Doors)
                // ==========================================
                bool roofEnabled = _loudnessCfg.EnableRoofCheck;
                bool windowEnabled = _loudnessCfg.EnableWindowCheck;
                bool doorEnabled = _loudnessCfg.EnableDoorCheck;
                bool randomEnabled = _loudnessCfg.EnableRandomChance;
                bool loudnessEnabled = roofEnabled || windowEnabled || doorEnabled || randomEnabled;

                if (loudnessEnabled)
                {
                    // get current physical conditions
                    VehicleRoofState currentRoof = VehicleRoofState.Closed;
                    if (roofEnabled)
                    {
                        currentRoof = vehicle.RoofState;
                    }

                    VehicleDoor[] doors = Array.Empty<VehicleDoor>();
                    if (doorEnabled || windowEnabled)
                    {
                        doors = vehicle.Doors.ToArray();
                    }

                    // windows: check up to 4 windows (0..3)
                    bool currentAnyWindowBroken = false;
                    if (windowEnabled)
                    {
                        for (int w = 0; w < doors.Length; w++)
                        {
                            if (!Function.Call<bool>(Hash.IS_VEHICLE_WINDOW_INTACT, vehicle, w))
                            {
                                currentAnyWindowBroken = true;
                            }
                        }
                    }

                    // doors: create masks for open and broken states
                    int currentOpenMask = 0;
                    int currentBrokenMask = 0;
                    if (doorEnabled)
                    {
                        for (int d = 0; d < doors.Length && d < 20; d++)
                        {
                            VehicleDoor vd = doors[d];
                            if (vd.IsOpen || vd.IsFullyOpen)
                            {
                                currentOpenMask |= (1 << d);
                            }

                            if (vd.IsBroken)
                            {
                                currentBrokenMask |= (1 << d);
                            }
                        }
                    }

                    // fetch or create state
                    if (!_vehicleStates.TryGetValue(handle, out VehicleRadioState vstate))
                    {
                        vstate = new VehicleRadioState
                        {
                            PrevRoofState = currentRoof,
                            PrevAnyWindowBroken = currentAnyWindowBroken,
                            PrevOpenMask = currentOpenMask,
                            PrevBrokenMask = currentBrokenMask,
                            RandomChecked = false,
                            RandomResult = false,
                            LoudApplied = false,
                            LastRoofEnabled = roofEnabled,
                            LastWindowEnabled = windowEnabled,
                            LastDoorEnabled = doorEnabled,
                            LastRandomEnabled = randomEnabled
                        };
                    }

                    // detect re-evaluation triggers - ONLY check enabled features
                    bool roofChanged = roofEnabled && vstate.PrevRoofState != currentRoof;
                    bool windowsWentFromBrokenToAllIntact = windowEnabled && vstate.PrevAnyWindowBroken && !currentAnyWindowBroken;
                    bool doorOpenToClose = doorEnabled && (vstate.PrevOpenMask & ~currentOpenMask) != 0;
                    bool doorBrokenToFixed = doorEnabled && (vstate.PrevBrokenMask & ~currentBrokenMask) != 0;
                    bool reevalTriggered = roofChanged || windowsWentFromBrokenToAllIntact || doorOpenToClose || doorBrokenToFixed;

                    // Determine "guaranteed" condition now
                    bool guaranteed = false;
                    if (roofEnabled && (currentRoof == VehicleRoofState.Opened || currentRoof == VehicleRoofState.Opening))
                    {
                        guaranteed = true;
                    }

                    if (!guaranteed && windowEnabled && currentAnyWindowBroken)
                    {
                        guaranteed = true;
                    }

                    if (!guaranteed && doorEnabled)
                    {
                        for (int d = 0; d < doors.Length; d++)
                        {
                            VehicleDoor vd = doors[d];
                            if (vd.IsOpen || vd.IsBroken || vd.IsFullyOpen)
                            {
                                guaranteed = true;
                                break;
                            }
                        }
                    }

                    // Evaluate loudness decision
                    // NEW in V1.2.0: check if config changed then reevaluate
                    bool configChanged =
                        vstate.LastRoofEnabled != roofEnabled ||
                        vstate.LastWindowEnabled != windowEnabled ||
                        vstate.LastDoorEnabled != doorEnabled ||
                        vstate.LastRandomEnabled != randomEnabled;

                    if (!vstate.RandomChecked || reevalTriggered || configChanged)
                    {
                        bool finalLoud;
                        if (guaranteed)
                        {
                            finalLoud = true;
                            vstate.RandomResult = true;
                        }
                        else if (randomEnabled)
                        {
                            bool rnd = MathFunctions.NextInt(0, 100) < _loudnessCfg.RandomLoudRadioChancePercentage;
                            finalLoud = rnd;
                            vstate.RandomResult = rnd;
                        }
                        else
                        {
                            finalLoud = false;
                            vstate.RandomResult = false;
                        }
                        vstate.RandomChecked = true;

                        if (vstate.LoudApplied != finalLoud)
                        {
                            Function.Call(Hash.SET_VEHICLE_RADIO_LOUD, handle, finalLoud);
                            vstate.LoudApplied = finalLoud;

                            _touchedLoud.Add(handle);
                        }
                    }

                    // update previous physical state for next tick
                    vstate.PrevRoofState = roofEnabled ? currentRoof : vstate.PrevRoofState;
                    vstate.PrevAnyWindowBroken = windowEnabled ? currentAnyWindowBroken : vstate.PrevAnyWindowBroken;
                    vstate.PrevOpenMask = doorEnabled ? currentOpenMask : vstate.PrevOpenMask;
                    vstate.PrevBrokenMask = doorEnabled ? currentBrokenMask : vstate.PrevBrokenMask;

                    // NEW in V1.2.0: remember the enabled-state we just used
                    vstate.LastRoofEnabled = roofEnabled;
                    vstate.LastWindowEnabled = windowEnabled;
                    vstate.LastDoorEnabled = doorEnabled;
                    vstate.LastRandomEnabled = randomEnabled;

                    // save state back
                    _vehicleStates[handle] = vstate;
                }
                else
                {
                    // NEW in V1.2.0: If loud was applied before, turn it off before cleaning up
                    if (_vehicleStates.TryGetValue(handle, out VehicleRadioState vstate) && vstate.LoudApplied)
                    {
                        Function.Call(Hash.SET_VEHICLE_RADIO_LOUD, handle, false);
                    }

                    // IMPORTANT: Clean up dictionary to prevent memory leaks if features are toggled off
                    _vehicleStates.Remove(handle);
                }

                // ==========================================
                // 2 & 3. TUNNEL AND WATER HANDLING (Combined)
                // ==========================================
                bool tunnelOrWaterEnabled = _mainCfg.DisableRadioInTunnels || _mainCfg.DisableRadioInWater;
                bool baseRadioEnabled = true;

                if (tunnelOrWaterEnabled)
                {
                    Vector3 vehiclePosition = vehicle.Position;

                    bool shouldDisableRadio = false;

                    // Short-circuit ensures we only check tunnels if the feature is enabled
                    if (_mainCfg.DisableRadioInTunnels && TunnelSpatialIndex.IsInside(vehiclePosition))
                    {
                        shouldDisableRadio = true;
                    }

                    // Short-circuit ensures we only check water if the feature is enabled
                    if (_mainCfg.DisableRadioInWater)
                    {
                        OutputArgument waterArg = new OutputArgument();

                        /* 
                        Check if vehicle is in water and deep enough for commercial FM
                        radio to usually mute in salted water
                        */
                        if (vehicle.IsInWater && Function.Call<bool>(Hash.GET_WATER_HEIGHT, vehiclePosition.X, vehiclePosition.Y, vehiclePosition.Z, waterArg))
                        {
                            float waterLevel = waterArg.GetResult<float>();
                            float depth = waterLevel - vehiclePosition.Z;

                            if (depth >= _mainCfg.RadioDisableDepth)
                            {
                                shouldDisableRadio = true;
                            }
                        }
                    }

                    baseRadioEnabled = !shouldDisableRadio;
                }

                // Thunderstorm interference should only temporarily disable radios
                // that would otherwise be enabled
                //
                // It does NOT override tunnel/water muting because if baseRadioEnabled
                // is already false, finalRadioEnabled remains false
                bool finalRadioEnabled = baseRadioEnabled && !_thunderInterferenceActive;

                if (_vehicleRadioEnabledStates.TryGetValue(handle, out bool lastRadioEnabled))
                {
                    // We have touched this vehicle before. Only update if the state actually changed.
                    if (lastRadioEnabled != finalRadioEnabled)
                    {
                        Function.Call(Hash.SET_VEHICLE_RADIO_ENABLED, handle, finalRadioEnabled);
                        _vehicleRadioEnabledStates[handle] = finalRadioEnabled;
                    }
                }
                else
                {
                    /* 
                       We have NEVER touched this vehicle's radio. 
                       Only intervene if we need to DISABLE it (e.g., they just drove into a tunnel).
                    */
                    if (!finalRadioEnabled)
                    {
                        Function.Call(Hash.SET_VEHICLE_RADIO_ENABLED, handle, false);
                        _vehicleRadioEnabledStates[handle] = false;

                        _touchedRadioEnabled.Add(handle);
                    }
                }
            } // end for vehicles

            // ==========================================
            // CLEANUP STALE STATES
            // ==========================================

            // Clean up _vehicleStates
            List<int> toRemoveStates = new List<int>();
            foreach (var kv in _vehicleStates)
            {
                if (!activeHandles.Contains(kv.Key))
                {
                    toRemoveStates.Add(kv.Key);
                }
            }
            for (int i = 0; i < toRemoveStates.Count; i++)
            {
                _vehicleStates.Remove(toRemoveStates[i]);
            }

            // Clean up _vehicleRadioEnabledStates (Tunnel/Water combined)
            List<int> toRemoveRadio = new List<int>();
            foreach (var kv in _vehicleRadioEnabledStates)
            {
                if (!activeHandles.Contains(kv.Key))
                {
                    toRemoveRadio.Add(kv.Key);
                }
            }
            for (int i = 0; i < toRemoveRadio.Count; i++)
            {
                _vehicleRadioEnabledStates.Remove(toRemoveRadio[i]);
            }
        }
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        Keys keyUp = e.KeyCode;
        string keyName = keyUp.ToString();

        // Toggle handling for debug drawing
        if (keyName == _debugCfg.DebugToggleKey)
        {
            TunnelDebugVisualizer.EnableDebug = !TunnelDebugVisualizer.EnableDebug;
            Screen.ShowHelpText($"~b~Tunnel Debug Drawing:~b~ {(TunnelDebugVisualizer.EnableDebug ? "~g~ON" : "~r~OFF")}", 2000, false);
        }

        // Toggle recording
        if (keyName == _recorderCfg.RecordToggleKey)
        {
            if (IsManualRecordingActive)
            {
                Screen.ShowHelpText("~r~You must first exit manual recording to use the recording key.", 3000, false);
                return;
            }

            TunnelRecorder.EnableTunnelRecorder = !TunnelRecorder.EnableTunnelRecorder;
            TunnelRecorder.HandleToggleKey();

            if (TunnelRecorder.EnableTunnelRecorder)
            {
                Screen.ShowSubtitle(
                    $"~b~Tunnel Recorder: ~g~Recording started ({keyUp} to stop)",
                    3000
                );
            }
            else
            {
                Screen.ShowHelpText(
                    $"~b~Tunnel Recorder: ~w~Recording finished. Samples: ~g~{TunnelRecorder.LastSavedAutomaticSampleCount}",
                    3000,
                    false
                );
            }
        }

        // Toggle menu
        if (keyName == _mainCfg.OpenMenuKey)
        {
            if (!UiMenuPool.AreAnyVisible)
            {
                _menus.MainMenu.Visible = !_menus.MainMenu.Visible;
            }
        }

        if (WaitingForKeyAssignment && keyUp != Keys.None)
        {
            WaitingForKeyAssignment = false;

            // Restore the correct menu
            if (PendingMenu != null)
            {
                PendingMenu.Visible = true;
            }

            // Update list selection (no allocations)
            if (PendingListItem != null)
            {
                int index = Array.IndexOf(KeysNames, keyName);
                if (index >= 0)
                {
                    PendingListItem.SelectedIndex = index;
                }

                PendingListItem = null;
            }
        }
    }

    private void UpdateThunderstormInterference()
    {
        if (!_thunderCfg.EnableThunderstormInterference)
        {
            if (_thunderInterferenceActive || _thunderState != ThunderInterferenceState.Inactive)
            {
                _thunderInterferenceActive = false;
                _thunderState = ThunderInterferenceState.Inactive;
            }

            return;
        }

        int currentWeatherHash = Function.Call<int>(Hash.GET_PREV_WEATHER_TYPE_HASH_NAME);

        if (currentWeatherHash != WEATHER_THUNDER_HASH)
        {
            if (_thunderInterferenceActive || _thunderState != ThunderInterferenceState.Inactive)
            {
                _thunderInterferenceActive = false;
                _thunderState = ThunderInterferenceState.Inactive;
            }

            return;
        }

        int now = Game.GameTime;

        /* 
        Thunderstorm just became active
        Schedule the first interference event
        */
        if (_thunderState == ThunderInterferenceState.Inactive)
        {
            _thunderInterferenceActive = false;
            _thunderState = ThunderInterferenceState.Waiting;

            float delay = RandomThunderSeconds(
                _thunderCfg.InitialInterferenceDelayMin,
                _thunderCfg.InitialInterferenceDelayMax
            );

            _nextThunderTransitionTime = now + (int)(delay * 1000f);
            return;
        }

        if (now < _nextThunderTransitionTime)
        {
            return;
        }

        if (_thunderState == ThunderInterferenceState.Waiting)
        {
            // Start interference
            _thunderInterferenceActive = true;
            _thunderState = ThunderInterferenceState.Interfering;

            float duration = RandomThunderSeconds(
                _thunderCfg.InterferenceDurationMin,
                _thunderCfg.InterferenceDurationMax
            );

            _nextThunderTransitionTime = now + (int)(duration * 1000f);
        }
        else if (_thunderState == ThunderInterferenceState.Interfering)
        {
            // End interference
            _thunderInterferenceActive = false;
            _thunderState = ThunderInterferenceState.Waiting;

            float nextDelay = RandomThunderSeconds(
                _thunderCfg.NextInterferenceDelayMin,
                _thunderCfg.NextInterferenceDelayMax
            );

            _nextThunderTransitionTime = now + (int)(nextDelay * 1000f);
        }
    }

    private float RandomThunderSeconds(float min, float max)
    {
        if (max < min)
        {
            (max, min) = (min, max);
        }

        if (max <= 0f)
        {
            return 0f;
        }

        return (float)MathFunctions.NextDouble(min, max);
    }

    // Fixed limitaion found in V1.0.0
    private void RestoreVehicleRadioStates()
    {
        // 1. Restore Radio Enabled state
        foreach (int handle in _touchedRadioEnabled)
        {
            Vehicle veh = Entity.FromHandle(handle) as Vehicle;
            if (ValidFunctions.IsValidVehicle(veh))
            {
                // Reset to vanilla default (Enabled)
                Function.Call(Hash.SET_VEHICLE_RADIO_ENABLED, handle, true);
            }
        }

        // 2. Restore Loud state
        foreach (int handle in _touchedLoud)
        {
            Vehicle veh = Entity.FromHandle(handle) as Vehicle;
            if (ValidFunctions.IsValidVehicle(veh))
            {
                // Reset to vanilla default (Not Loud)
                Function.Call(Hash.SET_VEHICLE_RADIO_LOUD, handle, false);
            }
        }

        // 3. Clear the persistent sets
        _touchedRadioEnabled.Clear();
        _touchedLoud.Clear();

        // 4. Clear the active dictionaries
        _vehicleStates.Clear();
        _vehicleRadioEnabledStates.Clear();
    }
}