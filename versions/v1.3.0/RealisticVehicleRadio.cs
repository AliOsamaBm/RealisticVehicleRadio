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
using System.Threading.Tasks;
using System.Collections.Generic;

using static RealisticVehicleRadio.Menus;
using static RealisticVehicleRadio.Helpers;

using GTA;
using GTA.Math;
using GTA.Native;
using Input = GTA.Input;  // Does not exist in SHVDNE

namespace RealisticVehicleRadio
{
    public sealed class RealisticVehicleRadio : Script
    {
        private const int _ACTIVE_INTERVAL = 1;
        private const int _DEFAULT_INTERVAL = 100;

        // Timers. Uses Game.GameTime
        private int _nextLogicTime = 0;
        private int _nextScanTime = 0;
        private int _nextPerfMenuUpdateTime = 0;

        // Pre-sized collections in ctor to prevent reallocation overhead
        private readonly List<VehicleEntry> _entries;
        private readonly HashSet<int> _handles;

        // ==========================================
        // CACHED CONFIGURATION INSTANCES
        // ==========================================
        private readonly Configuration _config;
        private readonly Configuration.MainSettings _mainCfg;
        private readonly Configuration.PerformanceSettings _perfCfg;  // New in V1.3.0
        private readonly Configuration.LoudnessSettings _loudnessCfg;
        private readonly Configuration.TunnelDetectionSettings _tunnelCfg;
        private readonly Configuration.VehicleExclusions _exclusionsCfg;
        private readonly Configuration.TunnelDebugVisualizerSettings _debugCfg;
        private readonly Configuration.TunnelRecorderSettings _recorderCfg;
        private readonly Configuration.ThunderstormSettings _thunderCfg;

        private readonly Menus _menus;

        // New in V1.3.0: Thunderstorm interference state machine
        private readonly ThunderInterference _thunderInterference = new ThunderInterference();
        private readonly VehicleExclusionManager _exclusionManager = new VehicleExclusionManager();
        private readonly PlayerTunnelEffect _playerTunnelEffect = new PlayerTunnelEffect();
        private readonly LoudEchoManager _loudEchoManager = new LoudEchoManager();

        private VehicleEntry _playerVehicleEntry;
        private bool _hasPlayerVehicle = false;
        private int _lastPlayerVehicleHandle = -1;

        private float _lastAppliedGlobalSignal = 1f;

        private int _lastNearbyVehicleCount = 0;
        private bool _manualDebugWarningShown = false;

        public RealisticVehicleRadio()
        {
            MathFunctions.Seed((uint)DateTime.UtcNow.Ticks);

            // 1. Block startup thread until async file reads complete
            _config = FileOperations.LoadConfigurationAsync().GetAwaiter().GetResult();

            // Ensure Min <= Max pairs are consistent after deserialization
            _config.RealisticVehicleRadio.Thunderstorm.NormalizeMinMaxPairs();

            TunnelManager.LoadAsync().GetAwaiter().GetResult();

            // 2. Cache nested classes for high-performance access in OnTick
            _mainCfg = _config.RealisticVehicleRadio.Main;
            _perfCfg = _config.RealisticVehicleRadio.Performance;
            _loudnessCfg = _config.RealisticVehicleRadio.Loudness;
            _tunnelCfg = _config.RealisticVehicleRadio.TunnelDetection;
            _exclusionsCfg = _config.RealisticVehicleRadio.Exclusions;
            _debugCfg = _config.TunnelDebugVisualizer;
            _recorderCfg = _config.TunnelRecorder;
            _thunderCfg = _config.RealisticVehicleRadio.Thunderstorm;

            LoudnessEvaluator.Initialize(_perfCfg.MaxCachedVehicles);

            // Initialize the collections using the config value
            if (_perfCfg.MaxCachedVehicles > 0)
            {
                _entries = new List<VehicleEntry>(_perfCfg.MaxCachedVehicles);
                _handles = new HashSet<int>(_perfCfg.MaxCachedVehicles);
            }
            else
            {
                _entries = new List<VehicleEntry>();
                _handles = new HashSet<int>();
            }

            TunnelSpatialIndex.EnsureBuilt(
                TunnelManager.Tunnels,
                _tunnelCfg.TunnelHorizontalTolerance,
                _tunnelCfg.TunnelVerticalTolerance
            );

            _menus = new Menus(_config, _mainCfg, _perfCfg, _loudnessCfg, _tunnelCfg, _exclusionsCfg, _debugCfg, _recorderCfg, _thunderCfg, InvalidateExclusionCache);

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
            Function.Call(Hash.SET_GLOBAL_RADIO_SIGNAL_LEVEL, 1.0f);

            if (_hasPlayerVehicle && ValidFunctions.IsValidVehicle(_playerVehicleEntry.Vehicle))
            {
                RestoreVehicleState(ref _playerVehicleEntry);
            }

            for (int i = 0; i < _entries.Count; i++)
            {
                VehicleEntry entry = _entries[i];

                if (ValidFunctions.IsValidVehicle(entry.Vehicle))
                {
                    RestoreVehicleState(ref entry);
                }
            }

            _loudEchoManager.CleanupAll();

            LoudnessEvaluator.CleanupAllTemporaryPeds();

            _entries.Clear();
            _handles.Clear();

            TunnelManager.TunnelsChanged -= _menus.RefreshTunnelManagerMenuIfVisible;

            TunnelSaveQueue.FlushBlocking();
        }

        private void OnTick(object sender, EventArgs e)
        {
            // ==========================================
            // CACHE
            // ==========================================
            Ped playerPed = Game.Player?.Character;

            bool isPlayerPedValid = ValidFunctions.IsValidPed(playerPed);
            bool isPlayerExitingVehicle = isPlayerPedValid && playerPed.IsExitingVehicle;
            Vector3 playerPosition = isPlayerPedValid ? playerPed.Position : Vector3.Zero;

            Vehicle currentPlayerVehicle = isPlayerPedValid ? playerPed.CurrentVehicle : null;
            bool isPlayerVehicleValid = ValidFunctions.IsValidVehicle(currentPlayerVehicle);
            bool isInActiveVehicle = isPlayerVehicleValid && !isPlayerExitingVehicle;
            int currentPlayerVehicleHandle = isInActiveVehicle ? currentPlayerVehicle.Handle : -1;

            bool forceActiveForPlayerVehicle = isPlayerPedValid && (isPlayerExitingVehicle || isPlayerVehicleValid);

            int now = Game.GameTime;

            Interval = _playerTunnelEffect.IsActive ||
                    forceActiveForPlayerVehicle ||
                    TunnelDebugVisualizer.EnableDebug ||
                    TunnelRecorder.EnableTunnelRecorder ||
                    IsManualRecordingActive ||
                    UiMenuPool.AreAnyVisible ||
                    WaitingForKeyAssignment ||
                    KeybindOverlay.IsActive ? _ACTIVE_INTERVAL : _DEFAULT_INTERVAL;

            TunnelSaveQueue.Process();

            // ==========================================
            // UI & RENDERING (Runs EVERY FRAME)
            // ==========================================
            UiMenuPool.Process();

            // Update real-time stats for the performance menu
            if (now >= _nextPerfMenuUpdateTime)
            {
                /*
                Since both LogicUpdateIntervalMs and ScanIntervalMs have a minimum of 100ms, 
                the underlying data cannot change faster than every 100ms. Updating the display 
                more frequently than that would just show the same numbers repeatedly.  
                */
                _nextPerfMenuUpdateTime = now + 100;
                UpdatePerformanceMenuStats(playerPosition, isPlayerPedValid);
            }

            GUI.ProcessMessages();

            if (WaitingForKeyAssignment)
            {
                // Disable controls
                Input.Controls.DisableAllControlActionsThisFrame(Input.ControlType.PlayerControl);
                // Use Game.DisableAllControlsThisFrame(); while using SHVDNE

                // Hide the specific menu that triggered the keybind
                if (PendingMenu != null)
                {
                    PendingMenu.Visible = false;
                }

                // Hide all hud components
                for (int i = 1; i <= 22; i++)
                {
                    Function.Call(Hash.HIDE_HUD_COMPONENT_THIS_FRAME, i);
                }
                Function.Call(Hash.DISABLE_FRONTEND_THIS_FRAME);
                Function.Call(Hash.HIDE_HUD_AND_RADAR_THIS_FRAME);
            }

            KeybindOverlay.UpdateAndDraw();

            // ==========================================
            // PLAYER VEHICLE TRANSITION LOGIC
            // ==========================================
            // Player entered a vehicle or switched vehicles
            if (isInActiveVehicle && currentPlayerVehicleHandle != _lastPlayerVehicleHandle)
            {
                // 1. Demote previous player vehicle to NPC cache
                if (_lastPlayerVehicleHandle != -1 && _hasPlayerVehicle)
                {
                    ReturnPlayerVehicleToNpcCache();
                }

                // 2. Promote new vehicle to player vehicle
                bool foundInCache = false;
                for (int i = _entries.Count - 1; i >= 0; i--)
                {
                    if (_entries[i].Handle == currentPlayerVehicleHandle)
                    {
                        _playerVehicleEntry = _entries[i];
                        _entries.RemoveAt(i);
                        foundInCache = true;
                        break;
                    }
                }

                if (!foundInCache)
                {
                    _playerVehicleEntry = new VehicleEntry
                    {
                        Vehicle = currentPlayerVehicle,
                        Handle = currentPlayerVehicleHandle,
                        IsRadioEnabled = true,
                        RadioState = default,
                        LastKnownStation = null
                    };
                    _handles.Add(currentPlayerVehicleHandle); // Prevent scanner from adding it
                }

                _hasPlayerVehicle = true;
                _lastPlayerVehicleHandle = currentPlayerVehicleHandle;
            }
            // Player exited vehicle or vehicle was destroyed
            else if (!isInActiveVehicle && _lastPlayerVehicleHandle != -1)
            {
                if (_hasPlayerVehicle)
                {
                    ReturnPlayerVehicleToNpcCache();
                }

                _hasPlayerVehicle = false;
                _lastPlayerVehicleHandle = -1;
            }

            _thunderInterference.Update(_thunderCfg, now);

            if (_hasPlayerVehicle)
            {
                _playerTunnelEffect.Update(playerPed, _playerVehicleEntry, _mainCfg);
            }

            // ==========================================
            // CENTRALIZED GLOBAL SIGNAL MANAGER
            // ==========================================
            // The worst signal wins (lowest value = most static)
            float finalSignalLevel = Math.Min(_thunderInterference.TargetSignalLevel, _playerTunnelEffect.TargetSignalLevel);

            // Only call the native if the value has actually changed
            if (Math.Abs(finalSignalLevel - _lastAppliedGlobalSignal) > 0.001f)
            {
                Function.Call(Hash.SET_GLOBAL_RADIO_SIGNAL_LEVEL, finalSignalLevel);
                _lastAppliedGlobalSignal = finalSignalLevel;
            }

            // ==========================================
            // RADIO AMPLIFIER VEHICLES
            // ==========================================

            // The vehicle we are checking right now:
            // - current vehicle if player is inside
            // - last vehicle if player is outside
            Vehicle echoVehicle = isInActiveVehicle
                ? currentPlayerVehicle
                : (isPlayerPedValid ? playerPed.LastVehicle : null);

            bool echoVehicleValid = ValidFunctions.IsValidVehicle(echoVehicle);
            int echoVehicleHandle = echoVehicleValid ? echoVehicle.Handle : -1;

            bool echoVehicleHasLoudProperty = echoVehicleValid && HasVehicleLoudProperty(echoVehicleHandle);
            bool echoVehicleRadioEnabled = echoVehicleValid && IsVehicleRadioEnabled(echoVehicleHandle);

            // Thunderstorm condition.
            bool thunderBlocksThisVehicle =  _thunderInterference.IsActive;

            // Tunnel condition.
            bool tunnelBlocksThisVehicle = _playerTunnelEffect.ShouldMuteRadio;

            // If the player is outside, PlayerTunnelEffect may already be restored.
            // So also check the relevant vehicle position directly.
            if (!tunnelBlocksThisVehicle &&
                echoVehicleValid &&
                (_mainCfg.MuteRadioInTunnels || _mainCfg.EnableStaticRadioInTunnels))
            {
                tunnelBlocksThisVehicle = TunnelSpatialIndex.IsInside(echoVehicle.Position);
            }

            bool relevantVehicleAllowed =
                echoVehicleValid &&
                echoVehicleHasLoudProperty &&
                echoVehicleRadioEnabled &&
                !thunderBlocksThisVehicle &&
                !tunnelBlocksThisVehicle;

            /*
            IMPORTANT:

            echoCount is now ONLY the configured global count.

            Do NOT set this to zero because of tunnel/thunder/loud failure.
            Vehicle-specific failure is handled by relevantVehicleAllowed.
            */
            int echoCount = _loudnessCfg.LoudEchoVehicleCount;

            _loudEchoManager.Update(
                playerPed,
                currentPlayerVehicle,
                relevantVehicleAllowed,
                echoCount,
                now);

            // ==========================================
            // DEBUG & RECORDERS
            // ==========================================
            if (isPlayerPedValid)
            {
                bool isDebugEnabled = TunnelDebugVisualizer.EnableDebug;

                if (isDebugEnabled)
                {
                    // Draw all saved tunnels individually to prevent cross-map lines
                    TunnelDebugVisualizer.DrawAllTunnels(playerPosition, TunnelManager.Tunnels, _debugCfg);

                    // Smoothly draw vehicle projections
                    if (isPlayerVehicleValid)
                    {
                        TunnelDebugVisualizer.DrawVehicleProjectionDebug(currentPlayerVehicle.Position, TunnelManager.Tunnels, now);
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

                if (IsManualRecordingActive)
                {
                    if (isPlayerVehicleValid)
                    {
                        // New in V1.3.0: Clear area every frame while manual recording is active
                        TunnelRecorder.EnsureNoInterferingPedsAndVehicles(playerPed, currentPlayerVehicle, playerPosition, _recorderCfg);

                        if (isDebugEnabled)
                        {
                            _manualDebugWarningShown = false;

                            TunnelPoint? previewPoint = TunnelRecorder.ComputePreviewPoint(currentPlayerVehicle, _recorderCfg);
                            if (previewPoint.HasValue)
                            {
                                TunnelDebugVisualizer.DrawSinglePointPreview(previewPoint, playerPosition, _debugCfg);
                            }
                        }
                        else if (!_manualDebugWarningShown)
                        {
                            GUI.ShowContextualMessage("~b~Manual Recording:~b~ ~o~Enable debug mode to see preview.", 3000);
                            _manualDebugWarningShown = true;
                        }
                    }
                    else
                    {
                        _manualDebugWarningShown = false;

                        if (TunnelRecorder.ManualPointCount >= 2)
                        {
                            TunnelRecorder.TrySetManualRecording(false);
                            TunnelRecorder.SaveManualChanges(vehicleLost: true);
                        }
                        else if (TunnelRecorder.ManualPointCount > 0)
                        {
                            TunnelRecorder.TrySetManualRecording(false);
                            TunnelRecorder.ClearManualPoints(vehicleLost: true);
                        }
                        else
                        {
                            TunnelRecorder.TrySetManualRecording(false);
                            GUI.ShowContextualMessage("~b~Manual Recording:~b~ ~o~Vehicle lost. Manual recording mode off.", 3000);
                        }
                    }
                }
                else if (_manualDebugWarningShown)
                {
                    _manualDebugWarningShown = false;            // <-- A: MR off (already present)
                }

                if (TunnelRecorder.EnableTunnelRecorder)
                {
                    if (!isInActiveVehicle)
                    {
                        // Was recording, but vehicle became invalid
                        TunnelRecorder.EnableTunnelRecorder = false;
                        TunnelRecorder.HandleToggleKey(vehicleLost: true);
                    }
                    else
                    {
                        TunnelRecorder.Record(playerPed, currentPlayerVehicle, playerPosition, _recorderCfg);
                    }
                }
            }

            // ==================================================================
            // CACHED VEHICLE PROCESSING (Throttled to configured value)
            // ==================================================================
            if (now >= _nextLogicTime)
            {
                _nextLogicTime = now + _perfCfg.LogicUpdateIntervalMs;

                // Build spatial index if ANY tunnel feature or debug is enabled
                if (_mainCfg.MuteRadioInTunnels || _mainCfg.EnableStaticRadioInTunnels || TunnelDebugVisualizer.EnableDebug)
                {
                    TunnelSpatialIndex.EnsureBuilt(
                        TunnelManager.Tunnels,
                        _tunnelCfg.TunnelHorizontalTolerance,
                        _tunnelCfg.TunnelVerticalTolerance
                    );
                }

                if (_hasPlayerVehicle)
                {
                    if (!ValidFunctions.IsValidVehicle(_playerVehicleEntry.Vehicle))
                    {
                        RestoreVehicleState(ref _playerVehicleEntry);
                        _handles.Remove(_playerVehicleEntry.Handle);
                        _hasPlayerVehicle = false;
                        _lastPlayerVehicleHandle = -1;
                    }
                    else
                    {
                        // Store latest player station before mute/unmute processing.
                        CachePlayerVehicleStation(clearIfRadioOff: true);

                        /*
                        Process player vehicle, telling the evaluator to ignore normal tunnel muting 
                        and respect the special logic's mute state.
                        */
                        ProcessSingleVehicle(ref _playerVehicleEntry, isPlayer: true);
                    }
                }

                bool removeDistanceEnabled = _perfCfg.RemoveDistance > 0;
                float removeDistSqr = removeDistanceEnabled
                    ? _perfCfg.RemoveDistance * _perfCfg.RemoveDistance
                    : 0f;

                bool processDistanceEnabled = _perfCfg.MaxProcessDistance > 0;
                float processDistSqr = processDistanceEnabled
                    ? _perfCfg.MaxProcessDistance * _perfCfg.MaxProcessDistance
                    : 0f;

                // Backward iteration allows O(1) removals without shifting elements
                for (int i = _entries.Count - 1; i >= 0; i--)
                {
                    VehicleEntry entry = _entries[i];
                    if (!ValidFunctions.IsValidVehicle(entry.Vehicle))
                    {
                        RestoreVehicleState(ref entry);
                        _entries.RemoveAt(i);
                        _handles.Remove(entry.Handle);
                        continue;
                    }

                    if (isPlayerPedValid)
                    {
                        Vector3 vehPos = entry.Vehicle.Position;

                        float dx = vehPos.X - playerPosition.X;
                        float dy = vehPos.Y - playerPosition.Y;
                        float dz = vehPos.Z - playerPosition.Z;

                        float distSqr = (dx * dx) + (dy * dy) + (dz * dz);

                        // RemoveDistance <= 0 disables distance-based removal
                        if (removeDistanceEnabled && distSqr > removeDistSqr)
                        {
                            RestoreVehicleState(ref entry);
                            _entries.RemoveAt(i);
                            _handles.Remove(entry.Handle);
                            continue;
                        }

                        if (processDistanceEnabled && distSqr > processDistSqr)
                        {
                            continue; // Keep cached, but skip expensive processing
                        }
                    }

                    ProcessSingleVehicle(ref entry);
                    _entries[i] = entry;
                }
            }

            // ===========================================================================
            // PERIODIC SCANNING FOR NEW VEHICLES (Throttled to configured value)
            // ===========================================================================
            if (now >= _nextScanTime)
            {
                _nextScanTime = now + _perfCfg.ScanIntervalMs;

                // If MaxCachedVehicles <= 0, treat it as unlimited (ignore the limit)
                bool isCacheFull = _perfCfg.MaxCachedVehicles > 0 && _entries.Count >= _perfCfg.MaxCachedVehicles;

                if (!isCacheFull)
                {
                    // Use distance-based vehicle search; set VehiclesSearchDistance <= 0 to include all vehicles
                    Vehicle[] nearbyVehicles = isPlayerPedValid && _perfCfg.VehiclesSearchDistance > 0
                        ? World.GetNearbyVehicles(playerPosition, _perfCfg.VehiclesSearchDistance)
                        : World.GetAllVehicles();

                    if (nearbyVehicles != null && nearbyVehicles.Length > 0)
                    {
                        _lastNearbyVehicleCount = nearbyVehicles.Length; // Save for the UI

                        // Determine how many vehicles to process based on the limit setting
                        int processLimit = _perfCfg.MaxProcessVehicles > 0
                            ? Math.Min(nearbyVehicles.Length, _perfCfg.MaxProcessVehicles)
                            : nearbyVehicles.Length;

                        for (int i = 0; i < processLimit; i++)
                        {
                            // Stop adding if we hit the limit (only applies if limit > 0)
                            if (_perfCfg.MaxCachedVehicles > 0 && _entries.Count >= _perfCfg.MaxCachedVehicles)
                            {
                                break;
                            }

                            Vehicle vehicle = nearbyVehicles[i];

                            if (!ValidFunctions.IsValidVehicle(vehicle))
                            {
                                continue;
                            }

                            int handle = vehicle.Handle;

                            if (_loudEchoManager.IsEchoVehicle(handle))
                            {
                                continue;
                            }

                            // HashSet prevents duplicate entries in O(1)
                            if (!_handles.Add(handle))
                            {
                                continue;
                            }

                            _entries.Add(new VehicleEntry
                            {
                                Vehicle = vehicle,
                                Handle = handle,
                                IsRadioEnabled = true,
                                RadioState = default,
                                LastKnownStation = null
                            });
                        }
                    }
                }
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            // Fixed in V1.3.0: Misleading name
            Keys keyCode = e.KeyCode;
            string keyName = keyCode.ToString();

            if (WaitingForKeyAssignment)
            {
                if (keyCode == Keys.Escape)
                {
                    WaitingForKeyAssignment = false;
                    PendingMenu.Visible = true;
                    PendingListItem = null;
                    GUI.ShowContextualMessage("~y~Key binding cancelled.", 2000);
                    return;
                }

                if (keyCode != Keys.None)
                {
                    WaitingForKeyAssignment = false;
                    PendingMenu.Visible = true;

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

                return;
            }

            // Toggle handling for debug drawing
            if (keyName == _debugCfg.DebugToggleKey)
            {
                TunnelDebugVisualizer.EnableDebug = !TunnelDebugVisualizer.EnableDebug;
                GUI.ShowContextualMessage($"~b~Tunnel Debug Drawing:~b~ {(TunnelDebugVisualizer.EnableDebug ? "~g~ON" : "~r~OFF")}", 3000);
            }

            // Toggle recording
            if (keyName == _recorderCfg.RecordToggleKey)
            {
                if (IsManualRecordingActive)
                {
                    GUI.ShowContextualMessage("~b~Tunnel Recorder: ~r~Exit manual mode first.", 3000);
                    return;
                }

                Ped playerPed = Game.Player.Character;
                bool isInValidVehicle = ValidFunctions.IsValidPed(playerPed)
                    && ValidFunctions.IsValidVehicle(playerPed.CurrentVehicle)
                    && !playerPed.IsExitingVehicle;

                // 1. Trying to START but no valid vehicle
                if (!TunnelRecorder.EnableTunnelRecorder && !isInValidVehicle)
                {
                    GUI.ShowContextualMessage("~b~Tunnel Recorder: ~r~Enter a vehicle to record.", 3000);
                    return;
                }

                TunnelRecorder.EnableTunnelRecorder = !TunnelRecorder.EnableTunnelRecorder;
                TunnelRecorder.HandleToggleKey();

                if (TunnelRecorder.EnableTunnelRecorder)
                {
                    GUI.ShowContextualMessage("~b~Tunnel Recorder: ~g~Recording started.", 3000);
                }
                // Note: If stopping, HandleToggleKey() automatically shows the "Saved" or "Need 2+ points" message.
            }

            // Toggle menu
            if (keyName == _mainCfg.OpenMenuKey)
            {
                if (!UiMenuPool.AreAnyVisible)
                {
                    _menus.MainMenu.Visible = !_menus.MainMenu.Visible;
                }
            }
        }

        // Helpers
        private void RestoreVehicleState(ref VehicleEntry entry)
        {
            if (entry.RadioState.LoudApplied)
            {
                Function.Call(Hash.SET_VEHICLE_RADIO_LOUD, entry.Handle, false);
            }

            if (!entry.IsRadioEnabled)
            {
                Function.Call(Hash.SET_VEHICLE_RADIO_ENABLED, entry.Handle, true);
            }
        }

        private void ProcessSingleVehicle(ref VehicleEntry entry, bool isPlayer = false)
        {
            Vehicle vehicle = entry.Vehicle;

            /*
            Fixed in V1.2.2: Vehicle enters tunnel -> Engine off -> Somehow without turning the engine 
            back on, the vehicle got outside tunnel -> Bug: its radio will be disabled.
            Radio is off already by default when engine is not running, so no need to procced further.
            Restore its default state if we previously modified it.
            */
            if (!vehicle.IsEngineRunning)
            {
                if (!entry.IsRadioEnabled)
                {
                    Function.Call(Hash.SET_VEHICLE_RADIO_ENABLED, entry.Handle, true);
                    entry.IsRadioEnabled = true;

                    if (!string.IsNullOrWhiteSpace(entry.LastKnownStation))
                    {
                        Function.Call(Hash.SET_VEH_RADIO_STATION, entry.Handle, entry.LastKnownStation);
                    }
                }
                if (entry.RadioState.LoudApplied)
                {
                    Function.Call(Hash.SET_VEHICLE_RADIO_LOUD, entry.Handle, false);
                    entry.RadioState.LoudApplied = false;
                }
                return;
            }

            // ==========================================
            // 1. LAZY COMPUTE EXCLUSION (Cached per entry)
            // ==========================================
            if (!entry.ExclusionComputed)
            {
                VehicleHash modelHash = (VehicleHash)vehicle.Model.Hash;

                entry.IsExcluded = _exclusionManager.IsVehicleExcluded(_exclusionsCfg.VehiclesWithNoRadio, modelHash) ||
                    (_exclusionsCfg.DisableRadioForMotorcycles && vehicle.IsMotorcycle) ||
                    (_exclusionsCfg.DisableRadioForPlanes && vehicle.IsPlane) ||
                    (_exclusionsCfg.DisableRadioForAircraft && vehicle.IsAircraft) ||
                    (_exclusionsCfg.DisableRadioForQuadBikes && vehicle.IsQuadBike) ||
                    (_exclusionsCfg.DisableRadioForHelicopters && vehicle.IsHelicopter) ||
                    (_exclusionsCfg.DisableRadioForTrains && vehicle.IsTrain) ||
                    (_exclusionsCfg.DisableRadioForSubmarines && vehicle.IsSubmarine) ||
                    (_exclusionsCfg.DisableRadioForSubmarineCars && vehicle.IsSubmarineCar) ||
                    (_exclusionsCfg.DisableRadioForRegularQuadBikes && vehicle.IsRegularQuadBike) ||
                    (_exclusionsCfg.DisableRadioForMilitaryVehicles && vehicle.ClassType == VehicleClass.Military);

                entry.ExclusionComputed = true;
            }

            // ==========================================
            // 2. BRANCH ON CACHED EXCLUSION RESULT
            // ==========================================
            if (entry.IsExcluded)
            {
                // Only call natives when the state actually changes
                if (entry.IsRadioEnabled)
                {
                    Function.Call(Hash.SET_VEHICLE_RADIO_ENABLED, entry.Handle, false);
                    entry.IsRadioEnabled = false;
                }
                // Restore the default radio volume state if it was previously modified
                if (entry.RadioState.LoudApplied)
                {
                    Function.Call(Hash.SET_VEHICLE_RADIO_LOUD, entry.Handle, false);
                    entry.RadioState = default;
                }
                return;
            }

            if (isPlayer)
            {
                /* 
                Only bypass normal tunnel muting if the special static logic is handling it.
                If static is disabled, let normal logic handle instant cutoff (T F behavior).
                */

                bool bypassNormalTunnelMuting = _mainCfg.EnableStaticRadioInTunnels;
                LoudnessEvaluator.Evaluate(vehicle, _thunderInterference, _mainCfg, _loudnessCfg, _playerTunnelEffect, ref entry, ignoreTunnelMute: bypassNormalTunnelMuting, forceRadioOff: _playerTunnelEffect.ShouldMuteRadio);
            }
            else
            {
                LoudnessEvaluator.Evaluate(vehicle, _thunderInterference, _mainCfg, _loudnessCfg, _playerTunnelEffect, ref entry);
            }
        }

        private void ReturnPlayerVehicleToNpcCache()
        {
            // Save station one more time before this vehicle stops being the active player vehicle.
            CachePlayerVehicleStation(clearIfRadioOff: false);

            /*
            Stop player-only tunnel static.
            Do NOT force the radio enabled here. Let normal NPC logic decide.
            */

            _playerTunnelEffect.RestoreFx();

            if (!ValidFunctions.IsValidVehicle(_playerVehicleEntry.Vehicle))
            {
                RestoreVehicleState(ref _playerVehicleEntry);
                _handles.Remove(_playerVehicleEntry.Handle);
                return;
            }

            // Safety check: if it is somehow already cached, update it instead of duplicating it.
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Handle == _playerVehicleEntry.Handle)
                {
                    ProcessSingleVehicle(ref _playerVehicleEntry);
                    _entries[i] = _playerVehicleEntry;
                    return;
                }
            }

            // Apply normal NPC rules once immediately so it behaves like NPC vehicles right away.
            ProcessSingleVehicle(ref _playerVehicleEntry);

            // The handle is already inside _handles, so just add the entry.
            _entries.Add(_playerVehicleEntry);
        }

        private void InvalidateExclusionCache()
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                VehicleEntry entry = _entries[i];

                // ONLY reset the exclusion and decision cache flags
                entry.ExclusionComputed = false;
                entry.IsExcluded = false;
                entry.RadioState.DecisionEvaluated = false; // Forces loudness re-evaluation

                _entries[i] = entry;
            }

            if (_hasPlayerVehicle)
            {
                _playerVehicleEntry.ExclusionComputed = false;
                _playerVehicleEntry.IsExcluded = false;
                _playerVehicleEntry.RadioState.DecisionEvaluated = false;
            }
        }

        private void UpdatePerformanceMenuStats(Vector3 playerPosition, bool isPlayerPedValid)
        {
            if (!_menus._performanceSettingsMenu.Visible)
            {
                return;
            }

            var items = _menus._performanceSettingsMenu.Items;

            // Loop exactly 7 times for the settings (ignoring the Save/Reset buttons at the end)
            for (int i = 0; i < 7; i++)
            {
                if (i == 1 || i == 3)
                {
                    continue; // Skip "scan" and "logic" intervals
                }

                var item = items[i];

                if (i == 0)
                {
                    item.Description = $"Adjust how far, in meters, the script searches for vehicles. ~y~Set to 0 to search all vehicles regardless of distance. Press the save button to apply changes. ~o~Current Found: {_lastNearbyVehicleCount}";
                }
                else if (i == 2)
                {
                    int processLimit = _perfCfg.MaxProcessVehicles > 0
                        ? Math.Min(_lastNearbyVehicleCount, _perfCfg.MaxProcessVehicles)
                        : _lastNearbyVehicleCount;

                    item.Description = $"How many nearby vehicles are checked each time the script scans for new vehicles. ~y~Lower = better performance. Higher = faster detection. 0 = no limit. Press the save button to apply changes. ~o~Current Vehicles: {processLimit}";
                }
                else if (i == 4)
                {
                    int skippedCount = 0;
                    if (_perfCfg.MaxProcessDistance > 0 && isPlayerPedValid)
                    {
                        float processDistSqr = _perfCfg.MaxProcessDistance * _perfCfg.MaxProcessDistance;
                        for (int j = 0; j < _entries.Count; j++)
                        {
                            Vehicle veh = _entries[j].Vehicle;
                            if (!ValidFunctions.IsValidVehicle(veh))
                            {
                                continue;
                            }

                            Vector3 vehPos = veh.Position;
                            float dx = vehPos.X - playerPosition.X;
                            float dy = vehPos.Y - playerPosition.Y;
                            float dz = vehPos.Z - playerPosition.Z;

                            if ((dx * dx) + (dy * dy) + (dz * dz) > processDistSqr)
                            {
                                skippedCount++;
                            }
                        }
                    }
                    item.Description = $"Vehicles farther than this distance remain cached but skip expensive processing. ~y~Set to 0 or lower to process all cached vehicles regardless of distance. Press the save button to apply changes. ~o~Currently skipping: {skippedCount}";
                }
                else if (i == 5)
                {
                    item.Description = $"How many vehicles the script can keep tracked at once. ~y~Set to 0 for unlimited. Higher = more vehicles affected, but more CPU/memory usage. Press the save button to apply changes. ~o~Current Tracked: {_entries.Count}";
                }
                else if (i == 6)
                {
                    int outOfBoundsCount = 0;
                    if (_perfCfg.RemoveDistance > 0 && isPlayerPedValid)
                    {
                        float removeDistSqr = _perfCfg.RemoveDistance * _perfCfg.RemoveDistance;
                        for (int j = 0; j < _entries.Count; j++)
                        {
                            Vehicle veh = _entries[j].Vehicle;
                            if (!ValidFunctions.IsValidVehicle(veh))
                            {
                                continue;
                            }

                            Vector3 vehPos = veh.Position;
                            float dx = vehPos.X - playerPosition.X;
                            float dy = vehPos.Y - playerPosition.Y;
                            float dz = vehPos.Z - playerPosition.Z;

                            if ((dx * dx) + (dy * dy) + (dz * dz) > removeDistSqr)
                            {
                                outOfBoundsCount++;
                            }
                        }
                    }
                    item.Description = $"Vehicles farther than this distance are removed from the cache. ~y~Set to 0 to disable distance-based removal. If disabled, far-away vehicles remain cached and may fill the vehicle cache. Press the save button to apply changes. ~o~Pending removal: {outOfBoundsCount}";
                }
            }
        }

        private bool TryGetVehicleEntry(int handle, out VehicleEntry entry)
        {
            if (handle != -1)
            {
                if (_hasPlayerVehicle && _playerVehicleEntry.Handle == handle)
                {
                    entry = _playerVehicleEntry;
                    return true;
                }

                for (int i = _entries.Count - 1; i >= 0; i--)
                {
                    if (_entries[i].Handle == handle)
                    {
                        entry = _entries[i];
                        return true;
                    }
                }
            }

            entry = default;
            return false;
        }

        private bool HasVehicleLoudProperty(int handle)
        {
            return TryGetVehicleEntry(handle, out VehicleEntry entry) && entry.RadioState.LoudApplied;
        }

        private bool IsVehicleRadioEnabled(int handle)
        {
            return TryGetVehicleEntry(handle, out VehicleEntry entry) && entry.IsRadioEnabled;
        }

        private void CachePlayerVehicleStation(bool clearIfRadioOff)
        {
            if (!_hasPlayerVehicle || !ValidFunctions.IsValidVehicle(_playerVehicleEntry.Vehicle))
            {
                return;
            }

            Ped playerPed = Game.Player?.Character;

            if (!ValidFunctions.IsValidPed(playerPed) || playerPed.IsEnteringVehicle)
            {
                return;
            }

            string station = GetPlayerRadioStationName();

            if (!string.IsNullOrWhiteSpace(station))
            {
                _playerVehicleEntry.LastKnownStation = station;
            }
            else if (clearIfRadioOff && _playerVehicleEntry.IsRadioEnabled)
            {
                /*
                If the radio is enabled but the game reports no station,
                treat that as radio off.

                If the radio is currently disabled because of tunnel/water/thunderstorm
                muting, do NOT clear the cache. That would destroy the station
                you want to restore later.
                */
                _playerVehicleEntry.LastKnownStation = string.Empty;
            }
        }
    }

    // New in V1.3.0: Structs eliminate heap allocations entirely
    public struct VehicleEntry
    {
        public Vehicle Vehicle;
        public int Handle;
        public VehicleRadioState RadioState;
        public bool IsRadioEnabled;

        // New in V1.3.0: computed once on first ProcessSingleVehicle
        public bool IsExcluded;
        public bool ExclusionComputed;

        /*
        Cached radio station name for this vehicle.
        Only reliably known for vehicles that have been player vehicles.

        The reason this is not in VehicleRadioState
        is because VehicleRadioState gets reset with default in some places, which would wipe 
        the station cache.
        */
        public string LastKnownStation;
    }

    public struct VehicleRadioState
    {
        public VehicleRoofState PrevRoofState;
        public bool PrevAnyWindowBroken;
        public bool PrevAnyDoorBroken;

        // decision cache

        // Improved name in V1.3.0
        // True once a loudness decision has been evaluated for the current state
        public bool DecisionEvaluated;

        // Removed in V1.3.0: public bool RandomResult; since it was found useless

        public int LastRandomPercentage;

        // Whether we already applied loud to avoid duplicate native calls
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
}