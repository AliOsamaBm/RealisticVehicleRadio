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

using System.Collections.Generic;

using static RealisticVehicleRadio.Helpers;

using GTA;
using GTA.Math;
using GTA.Native;

namespace RealisticVehicleRadio
{
    public static class LoudnessEvaluator
    {
        // Reusable output argument to prevent per-frame allocations
        private static readonly OutputArgument _waterArg = new OutputArgument();
        private static List<Ped> _tempPedRemove;

        /// <summary>
        /// Initializes static collections for the evaluator. Call once on script startup.
        /// </summary>
        public static void Initialize(int maxCachedVehicles)
        {
            // If MaxCachedVehicles is 0 (unlimited)
            if (maxCachedVehicles > 0)
            {
                _tempPedRemove = new List<Ped>(maxCachedVehicles);
                return;
            }

            _tempPedRemove = new List<Ped>();
        }
        public static void Evaluate(Vehicle vehicle, ThunderInterference thunderInterference, Configuration.MainSettings mainCfg, Configuration.LoudnessSettings loudCfg, PlayerTunnelEffect playerTunnelEffect, ref VehicleEntry entry, bool ignoreTunnelMute = false, bool forceRadioOff = false)
        {
            // ==========================================
            // GHOST DRIVER CLEANUP
            // ==========================================
            /*
            Iterate backwards to safely remove items from the list without skipping 
            elements or causing index out-of-bounds exceptions. Swap-remove.
            */
            for (int i = 0; i < _tempPedRemove.Count;)
            {
                Ped tempPed = _tempPedRemove[i];

                if (ValidFunctions.IsValidPed(tempPed))
                {
                    tempPed.Delete();

                    int last = _tempPedRemove.Count - 1;
                    _tempPedRemove[i] = _tempPedRemove[last];
                    _tempPedRemove.RemoveAt(last);
                }
                else
                {
                    i++;
                }
            }

            // ==========================================
            // 1. LOUDNESS FEATURES (Roof, Windows, Doors)
            // ==========================================
            bool roofEnabled = loudCfg.EnableRoofCheck;
            bool windowEnabled = loudCfg.EnableWindowCheck;
            bool doorEnabled = loudCfg.EnableDoorCheck;
            bool randomEnabled = loudCfg.EnableRandomChance;
            int randomPercentage = loudCfg.RandomLoudRadioChancePercentage;
            bool loudnessEnabled = roofEnabled || windowEnabled || doorEnabled || randomEnabled;

            if (loudnessEnabled)
            {
                // get current physical conditions
                VehicleRoofState currentRoof = VehicleRoofState.Closed;
                if (roofEnabled)
                {
                    currentRoof = vehicle.RoofState;
                }

                bool currentAnyWindowBroken = windowEnabled && !vehicle.Windows.AllWindowsIntact;

                bool currentDoorBroken = false;
                if (doorEnabled)
                {
                    foreach (VehicleDoor door in vehicle.Doors)
                    {
                        // Fixed in V1.3.0: broken/open hood or trunk no longer trigger loud radio
                        VehicleDoorIndex doorIndex = door.Index;
                        if (doorIndex == VehicleDoorIndex.Hood || doorIndex == VehicleDoorIndex.Trunk)
                        {
                            continue;
                        }

                        if (door.IsOpen || door.IsFullyOpen || door.IsBroken)
                        {
                            currentDoorBroken = true;
                            break;
                        }
                    }
                }

                VehicleRadioState vstate = entry.RadioState;

                // Fixed in V1.2.1: detect re-evaluation triggers in BOTH directions (e.g., Intact->Broken AND Broken->Intact)
                bool roofChanged = roofEnabled && vstate.PrevRoofState != currentRoof;
                bool windowsChanged = windowEnabled && vstate.PrevAnyWindowBroken != currentAnyWindowBroken;
                bool doorsChanged = doorEnabled && vstate.PrevAnyDoorBroken != currentDoorBroken;
                bool reevalTriggered = roofChanged || windowsChanged || doorsChanged;

                // Determine "guaranteed" condition now
                bool guaranteed = false;
                if (roofEnabled && (currentRoof == VehicleRoofState.Opened || currentRoof == VehicleRoofState.Opening))
                {
                    guaranteed = true;
                }

                else if (windowEnabled && currentAnyWindowBroken)
                {
                    guaranteed = true;
                }

                else if (doorEnabled && currentDoorBroken)
                {
                    guaranteed = true;
                }

                // Evaluate loudness decision
                // NEW in V1.2.0: check if config changed then reevaluate
                bool configChanged =
                    vstate.LastRoofEnabled != roofEnabled ||
                    vstate.LastWindowEnabled != windowEnabled ||
                    vstate.LastDoorEnabled != doorEnabled ||
                    vstate.LastRandomEnabled != randomEnabled ||
                    vstate.LastRandomPercentage != randomPercentage;

                if (!vstate.DecisionEvaluated || reevalTriggered || configChanged)
                {
                    bool finalLoud;
                    if (guaranteed)
                    {
                        finalLoud = true;
                    }
                    else if (randomEnabled)
                    {
                        bool rnd = MathFunctions.NextInt(0, 100) < loudCfg.RandomLoudRadioChancePercentage;
                        finalLoud = rnd;
                    }
                    else
                    {
                        finalLoud = false;
                    }
                    vstate.DecisionEvaluated = true;

                    if (vstate.LoudApplied != finalLoud)
                    {
                        Function.Call(Hash.SET_VEHICLE_RADIO_LOUD, entry.Handle, finalLoud);
                        vstate.LoudApplied = finalLoud;
                    }
                }

                // update previous physical state for next tick
                vstate.PrevRoofState = roofEnabled ? currentRoof : vstate.PrevRoofState;
                vstate.PrevAnyWindowBroken = windowEnabled ? currentAnyWindowBroken : vstate.PrevAnyWindowBroken;
                vstate.PrevAnyDoorBroken = doorEnabled ? currentDoorBroken : vstate.PrevAnyDoorBroken;

                // NEW in V1.2.0: remember the enabled-state we just used
                vstate.LastRoofEnabled = roofEnabled;
                vstate.LastWindowEnabled = windowEnabled;
                vstate.LastDoorEnabled = doorEnabled;
                vstate.LastRandomEnabled = randomEnabled;
                vstate.LastRandomPercentage = randomPercentage;

                // save state back
                entry.RadioState = vstate;
            }
            else
            {
                // NEW in V1.2.0: If loud was applied before, turn it off before cleaning up
                if (entry.RadioState.LoudApplied)
                {
                    Function.Call(Hash.SET_VEHICLE_RADIO_LOUD, entry.Handle, false);
                    entry.RadioState.LoudApplied = false;
                }
                entry.RadioState = default; // Reset to prevent stale data
            }

            // =================================================
            // 2 & 3. TUNNEL AND WATER HANDLING (Combined)
            // =================================================
            bool tunnelOrWaterEnabled = mainCfg.MuteRadioInTunnels || mainCfg.DisableRadioInWater;
            bool baseRadioEnabled = true;

            if (tunnelOrWaterEnabled)
            {
                Vector3 vehiclePosition = vehicle.Position;
                bool shouldDisableRadio = false;

                // Short-circuit ensures we only check tunnels if the feature is enabled AND not ignored
                if (!ignoreTunnelMute && mainCfg.MuteRadioInTunnels && TunnelSpatialIndex.IsInside(vehiclePosition))
                {
                    shouldDisableRadio = true;
                }
                else if (ignoreTunnelMute && mainCfg.MuteRadioInTunnels && playerTunnelEffect.ShouldMuteRadio)
                {
                    shouldDisableRadio = true;
                }

                if (!shouldDisableRadio && mainCfg.DisableRadioInWater && vehicle.IsInWater)
                {
                    // Reuses class-level _waterArg to prevent allocation
                    if (Function.Call<bool>(Hash.GET_WATER_HEIGHT, vehiclePosition.X, vehiclePosition.Y, vehiclePosition.Z, _waterArg))
                    {
                        float waterLevel = _waterArg.GetResult<float>();
                        float depth = waterLevel - vehiclePosition.Z;

                        /* 
                        Check if vehicle is in water and deep enough for commercial FM
                        radio to usually mute in salted water
                        */
                        if (depth >= mainCfg.RadioDisableDepth)
                        {
                            shouldDisableRadio = true;
                        }
                    }
                }

                baseRadioEnabled = !shouldDisableRadio;
            }

            /*
            Thunderstorm interference should only temporarily disable radios
            that would otherwise be enabled. It does NOT override tunnel/water 
            muting because if baseRadioEnabled is already false, finalRadioEnabled remains false
            */
            bool finalRadioEnabled = baseRadioEnabled && !thunderInterference.IsActive && !forceRadioOff;
            if (entry.IsRadioEnabled != finalRadioEnabled)
            {
                /*
                =====================================================================
                WORKAROUND: The "Ghost Driver" Hack for Driverless Radio Unmuting
                =====================================================================

                THE BUG: When calling SET_VEHICLE_RADIO_ENABLED(handle, true/false) on an 
                NPC vehicle that has NO driver, the GTA V audio engine inexplicably 
                resets the vehicle's radio station to "OFF". 

                THE LIMITATION: There is no native (and no reliable memory offset) to 
                READ a vehicle's current radio station. The station data is stored in 
                the game's global CAudioManager singleton, not inside the CVehicle 
                memory structure. Therefore, we cannot simply save and restore the 
                station hash.

                THE FIX in V1.3.0: To prevent the engine from wiping the station during the 
                unmute/mute calls, we temporarily trick the game into thinking the vehicle 
                is occupied by spawning an invisible ped in the driver seat. Once the 
                native executes and the station is safely preserved, the ped is queued 
                for immediate deletion on the next evaluation tick.
                */

                if (!ValidFunctions.IsValidPed(vehicle.GetPedOnSeat(VehicleSeat.Driver)))
                {
                    Ped tempPed = SpawnInvisibleDriverPedFor(vehicle);
                    if (tempPed != null)
                    {
                        _tempPedRemove.Add(tempPed);
                    }
                }

                Function.Call(Hash.SET_VEHICLE_RADIO_ENABLED, entry.Handle, finalRadioEnabled);

                entry.IsRadioEnabled = finalRadioEnabled;

                // Restore cached station when unmuting.
                if (finalRadioEnabled && !string.IsNullOrWhiteSpace(entry.LastKnownStation))
                {
                    Function.Call(Hash.SET_VEH_RADIO_STATION, entry.Handle, entry.LastKnownStation);
                }
            }
        }

        public static void CleanupAllTemporaryPeds()
        {
            for (int i = 0; i < _tempPedRemove.Count; i++)
            {
                if (ValidFunctions.IsValidPed(_tempPedRemove[i]))
                {
                    _tempPedRemove[i].Delete();
                }
            }

            _tempPedRemove.Clear();
        }
    }
}