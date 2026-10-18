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
    public sealed class LoudEchoManager
    {
        // How often ghost properties are re-applied.
        private const int _MAINTAIN_INTERVAL_MS = 250;

        private sealed class EchoGroup
        {
            public string RadioStation;

            // ghost vehicle handle -> radio enabled state already applied
            public readonly Dictionary<int, bool> GhostVehicles = new Dictionary<int, bool>();
        }

        // original vehicle handle -> echo group
        private readonly Dictionary<int, EchoGroup> _ghostGroups = new Dictionary<int, EchoGroup>();

        // original vehicle handle -> last known radio station
        private readonly Dictionary<int, string> _lastKnownStationByVehicle = new Dictionary<int, string>();

        // Prevents repeated spawn attempts for the same exit event.
        private readonly HashSet<int> _processedVehicles = new HashSet<int>();

        // Allows the script to identify echo vehicles.
        private readonly HashSet<int> _echoVehicleHandles = new HashSet<int>();

        // Reusable buffers.
        private readonly List<int> _removalBuffer = new List<int>();
        private readonly List<int> _handleBuffer = new List<int>();

        private readonly HashSet<int> _blockedVehicles = new HashSet<int>();

        // Stores: (Temporary Ped, Ghost Vehicle, Radio Station, Time to Delete)
        private readonly List<(Ped ped, Vehicle vehicle, string station, int deleteTime)> _tempDriverPeds = new List<(Ped, Vehicle, string, int)>();
        
        // original vehicle handle -> vehicle/station spam entry
        private readonly List<(int originalHandle, Vehicle vehicle, string station, int deleteTime)> _stationSpamEntries = new List<(int, Vehicle, string, int)>();
        private int _nextMaintainTime;

        public bool IsEchoVehicle(int handle)
        {
            return _echoVehicleHandles.Contains(handle);
        }

        public void Update(Ped playerPed, Vehicle currentVehicle, bool isRelevantVehicleAllowed, int configuredEchoCount, int now)
        {
            MaintainGhostGroups(now);
            CleanupGhostGroups();
            CleanupProcessedVehicles();
            CleanupStationCache();
            CleanupBlockedVehicles();

            if (!ValidFunctions.IsValidPed(playerPed) || playerPed.IsDead)
            {
                CleanupAll();
                return;
            }

            if (!ValidFunctions.IsValidVehicle(currentVehicle))
            {
                currentVehicle = playerPed.CurrentVehicle;
            }

            bool isInTransition = playerPed.IsEnteringVehicle || playerPed.IsExitingVehicle;

            // ==========================================================
            // PLAYER IS INSIDE / EXITING A VEHICLE
            // ==========================================================
            if (ValidFunctions.IsValidVehicle(currentVehicle))
            {
                int currentHandle = currentVehicle.Handle;

                /*
                Cache station even if this specific vehicle is currently blocked.
                */
                if (!playerPed.IsEnteringVehicle)
                {
                    string station = GetPlayerRadioStationName();
                    _lastKnownStationByVehicle[currentHandle] = station ?? string.Empty;
                }

                if (configuredEchoCount <= 0)
                {
                    CleanupAllEchoGroups();
                    return;
                }

                if (!isRelevantVehicleAllowed)
                {
                    BlockVehicle(currentHandle);
                    return;
                }

                UnblockVehicle(currentHandle);
                RemoveGhostGroup(currentHandle, true);
                return;
            }

            if (configuredEchoCount <= 0)
            {
                CleanupAllEchoGroups();
                return;
            }

            // ==========================================================
            // PLAYER IS TRANSITIONING
            // ==========================================================
            if (isInTransition)
            {
                return;
            }

            // ==========================================================
            // PLAYER IS OUTSIDE
            // ==========================================================
            Vehicle lastVehicle = playerPed.LastVehicle;

            if (!ValidFunctions.IsValidVehicle(lastVehicle) ||
                lastVehicle.IsDead ||
                !lastVehicle.IsEngineRunning)
            {
                return;
            }

            int lastVehicleHandle = lastVehicle.Handle;

            if (!isRelevantVehicleAllowed)
            {
                BlockVehicle(lastVehicleHandle);
                return;
            }

            UnblockVehicle(lastVehicleHandle);

            if (_ghostGroups.TryGetValue(lastVehicleHandle, out EchoGroup existingGroup))
            {
                if (existingGroup.GhostVehicles.Count == configuredEchoCount)
                {
                    return;
                }

                RemoveGhostGroup(lastVehicleHandle, true);
            }

            if (_processedVehicles.Contains(lastVehicleHandle))
            {
                return;
            }

            if (!_lastKnownStationByVehicle.TryGetValue(lastVehicleHandle, out string stationForGhosts) ||
                string.IsNullOrWhiteSpace(stationForGhosts) || stationForGhosts == "OFF")
            {
                _processedVehicles.Add(lastVehicleHandle);
                return;
            }

            if (!_processedVehicles.Add(lastVehicleHandle))
            {
                return;
            }

            EchoGroup group = new EchoGroup
            {
                RadioStation = stationForGhosts
            };

            for (int i = 0; i < configuredEchoCount; i++)
            {
                Vehicle ghostVehicle = CreateGhostVehicle(lastVehicle, stationForGhosts);

                if (!ValidFunctions.IsValidVehicle(ghostVehicle))
                {
                    continue;
                }

                group.GhostVehicles[ghostVehicle.Handle] = true;
                _echoVehicleHandles.Add(ghostVehicle.Handle);
            }

            if (group.GhostVehicles.Count > 0)
            {
                _ghostGroups[lastVehicleHandle] = group;

                // Also spam the original last player vehicle station for a short time.
                AddStationSpam(lastVehicleHandle, lastVehicle, stationForGhosts, now + 2000);
            }
            else
            {
                _processedVehicles.Remove(lastVehicleHandle);
            }
        }

        public void CleanupAll()
        {
            foreach (EchoGroup group in _ghostGroups.Values)
            {
                foreach (KeyValuePair<int, bool> entry in group.GhostVehicles)
                {
                    DeleteEchoVehicle(entry.Key);
                }
            }

            foreach (var (ped, _, _, _) in _tempDriverPeds)
            {
                if (ValidFunctions.IsValidPed(ped))
                {
                    ped.Delete();
                }
            }
            _tempDriverPeds.Clear();

            _ghostGroups.Clear();
            _lastKnownStationByVehicle.Clear();
            _processedVehicles.Clear();
            _echoVehicleHandles.Clear();
            _removalBuffer.Clear();
            _handleBuffer.Clear();
            _blockedVehicles.Clear();
            _stationSpamEntries.Clear();
            _nextMaintainTime = 0;
        }

        // ==============================================================
        // INTERNAL
        // ==============================================================

        private void MaintainGhostGroups(int now)
        {
            CleanupTemporaryDriverPeds(now);
            CleanupStationSpam(now);

            if (_ghostGroups.Count == 0 && _tempDriverPeds.Count == 0 && _stationSpamEntries.Count == 0)
            {
                return;
            }

            if (now < _nextMaintainTime)
            {
                return;
            }

            _nextMaintainTime = now + _MAINTAIN_INTERVAL_MS;

            // Spam the original last player vehicle station.
            MaintainStationSpam();

            // Spam radio station for temporary drivers before they are deleted.
            for (int i = 0; i < _tempDriverPeds.Count; i++)
            {
                var (_, vehicle, station, _) = _tempDriverPeds[i];

                if (ValidFunctions.IsValidVehicle(vehicle) && !string.IsNullOrWhiteSpace(station))
                {
                    Function.Call(Hash.SET_VEH_RADIO_STATION, vehicle, station);
                    Function.Call(Hash.SET_VEHICLE_RADIO_ENABLED, vehicle.Handle, true);
                    Function.Call(Hash.SET_VEHICLE_RADIO_LOUD, vehicle.Handle, true);
                }
            }

            if (_ghostGroups.Count == 0)
            {
                return;
            }

            foreach (EchoGroup group in _ghostGroups.Values)
            {
                string station = group.RadioStation;

                foreach (KeyValuePair<int, bool> entry in group.GhostVehicles)
                {
                    Vehicle ghostVehicle = Entity.FromHandle(entry.Key) as Vehicle;

                    if (!ValidFunctions.IsValidVehicle(ghostVehicle))
                    {
                        continue;
                    }

                    ghostVehicle.IsCollisionEnabled = false;
                    ghostVehicle.IsVisible = false;
                    ghostVehicle.IsEngineRunning = true;

                    if (entry.Value)
                    {
                        Function.Call(Hash.SET_VEHICLE_RADIO_LOUD, ghostVehicle.Handle, true);
                    }
                }

                _handleBuffer.Clear();

                foreach (KeyValuePair<int, bool> entry in group.GhostVehicles)
                {
                    if (!entry.Value)
                    {
                        _handleBuffer.Add(entry.Key);
                    }
                }

                for (int i = 0; i < _handleBuffer.Count; i++)
                {
                    int ghostHandle = _handleBuffer[i];

                    Vehicle ghostVehicle = Entity.FromHandle(ghostHandle) as Vehicle;

                    if (!ValidFunctions.IsValidVehicle(ghostVehicle))
                    {
                        continue;
                    }

                    Ped tempPed = SpawnInvisibleDriverPedFor(ghostVehicle);

                    Function.Call(Hash.SET_VEHICLE_RADIO_ENABLED, ghostVehicle.Handle, true);

                    if (!string.IsNullOrWhiteSpace(station))
                    {
                        Function.Call(Hash.SET_VEH_RADIO_STATION, ghostVehicle, station);
                    }

                    Function.Call(Hash.SET_VEHICLE_RADIO_LOUD, ghostVehicle.Handle, true);

                    if (ValidFunctions.IsValidPed(tempPed))
                    {
                        _tempDriverPeds.Add((tempPed, ghostVehicle, station, now + 2000));
                    }

                    group.GhostVehicles[ghostHandle] = true;
                }
            }
        }

        private void CleanupGhostGroups()
        {
            _removalBuffer.Clear();

            foreach (KeyValuePair<int, EchoGroup> kvp in _ghostGroups)
            {
                int originalHandle = kvp.Key;
                EchoGroup group = kvp.Value;

                Vehicle originalVehicle = Entity.FromHandle(originalHandle) as Vehicle;

                bool originalGone = !ValidFunctions.IsValidVehicle(originalVehicle) || originalVehicle.IsDead;
                bool engineOff = !originalGone && !originalVehicle.IsEngineRunning;

                if (originalGone || engineOff)
                {
                    foreach (KeyValuePair<int, bool> entry in group.GhostVehicles)
                    {
                        DeleteEchoVehicle(entry.Key);
                    }

                    _removalBuffer.Add(originalHandle);

                    if (originalGone)
                    {
                        _processedVehicles.Remove(originalHandle);
                    }
                }
                else
                {
                    _handleBuffer.Clear();

                    foreach (KeyValuePair<int, bool> entry in group.GhostVehicles)
                    {
                        Vehicle ghostVehicle = Entity.FromHandle(entry.Key) as Vehicle;

                        if (!ValidFunctions.IsValidVehicle(ghostVehicle))
                        {
                            _handleBuffer.Add(entry.Key);
                        }
                    }

                    foreach (int ghostHandle in _handleBuffer)
                    {
                        group.GhostVehicles.Remove(ghostHandle);
                        _echoVehicleHandles.Remove(ghostHandle);
                    }

                    if (group.GhostVehicles.Count == 0)
                    {
                        _removalBuffer.Add(originalHandle);
                    }
                }
            }

            foreach (int handle in _removalBuffer)
            {
                _ghostGroups.Remove(handle);
            }
        }

        private void CleanupProcessedVehicles()
        {
            _removalBuffer.Clear();

            foreach (int handle in _processedVehicles)
            {
                Vehicle vehicle = Entity.FromHandle(handle) as Vehicle;

                if (!ValidFunctions.IsValidVehicle(vehicle) || vehicle.IsDead)
                {
                    _removalBuffer.Add(handle);
                }
            }

            foreach (int handle in _removalBuffer)
            {
                _processedVehicles.Remove(handle);
            }
        }

        private void CleanupStationCache()
        {
            _removalBuffer.Clear();

            foreach (int handle in _lastKnownStationByVehicle.Keys)
            {
                Vehicle vehicle = Entity.FromHandle(handle) as Vehicle;

                if (!ValidFunctions.IsValidVehicle(vehicle) || vehicle.IsDead)
                {
                    _removalBuffer.Add(handle);
                }
            }

            foreach (int handle in _removalBuffer)
            {
                _lastKnownStationByVehicle.Remove(handle);
            }
        }

        private void CleanupAllEchoGroups()
        {
            _removalBuffer.Clear();

            foreach (int handle in _ghostGroups.Keys)
            {
                _removalBuffer.Add(handle);
            }

            foreach (int handle in _removalBuffer)
            {
                RemoveGhostGroup(handle, true);
            }

            _processedVehicles.Clear();
        }

        private void RemoveGhostGroup(int originalVehicleHandle, bool unprocess)
        {
            RemoveStationSpam(originalVehicleHandle);

            if (_ghostGroups.TryGetValue(originalVehicleHandle, out EchoGroup group))
            {
                foreach (KeyValuePair<int, bool> entry in group.GhostVehicles)
                {
                    DeleteEchoVehicle(entry.Key);
                }

                _ghostGroups.Remove(originalVehicleHandle);
            }

            if (unprocess)
            {
                _processedVehicles.Remove(originalVehicleHandle);
            }
        }

        private Vehicle CreateGhostVehicle(Vehicle originalVehicle, string station)
        {
            Vehicle ghostVehicle = CreateGhostVehicleFor(originalVehicle);

            if (!ValidFunctions.IsValidVehicle(ghostVehicle))
            {
                return null;
            }

            Ped tempPed = SpawnInvisibleDriverPedFor(ghostVehicle);

            BreakAllVehicleDoorsAndWindows(ghostVehicle);

            Function.Call(Hash.SET_VEHICLE_RADIO_ENABLED, ghostVehicle.Handle, true);

            if (!string.IsNullOrWhiteSpace(station))
            {
                Function.Call(Hash.SET_VEH_RADIO_STATION, ghostVehicle, station);
            }

            Function.Call(Hash.SET_VEHICLE_RADIO_LOUD, ghostVehicle.Handle, true);

            // Queue for spamming and deletion on a later cleanup pass.
            if (ValidFunctions.IsValidPed(tempPed))
            {
                _tempDriverPeds.Add((tempPed, ghostVehicle, station, Game.GameTime + 2000));
            }

            return ghostVehicle;
        }

        private void DeleteEchoVehicle(int ghostVehicleHandle)
        {
            Vehicle ghostVehicle = Entity.FromHandle(ghostVehicleHandle) as Vehicle;

            if (ValidFunctions.IsValidVehicle(ghostVehicle))
            {
                Ped driver = ghostVehicle.Driver;

                if (ValidFunctions.IsValidPed(driver))
                {
                    driver.Delete();
                }

                ghostVehicle.Delete();
            }

            _echoVehicleHandles.Remove(ghostVehicleHandle);
        }

        private void BreakAllVehicleDoorsAndWindows(Vehicle vehicle)
        {
            try
            {
                foreach (VehicleDoor door in vehicle.Doors)
                {
                    if (door.Index == VehicleDoorIndex.Hood || door.Index == VehicleDoorIndex.Trunk)
                    {
                        door.Break(false);
                    }
                }

                if (vehicle.IsConvertible)
                {
                    vehicle.RoofState = VehicleRoofState.Opened;
                }

                vehicle.Windows[VehicleWindowIndex.BackLeftWindow].Smash();
                vehicle.Windows[VehicleWindowIndex.BackRightWindow].Smash();
                vehicle.Windows[VehicleWindowIndex.BackWindshield].Smash();
                vehicle.Windows[VehicleWindowIndex.FrontLeftWindow].Smash();
                vehicle.Windows[VehicleWindowIndex.FrontRightWindow].Smash();
                vehicle.Windows[VehicleWindowIndex.MiddleLeftWindow].Smash();
                vehicle.Windows[VehicleWindowIndex.MiddleRightWindow].Smash();
                vehicle.Windows[VehicleWindowIndex.Windshield].Smash();
            }
            catch
            {
                // Some vehicles do not have every window index.
            }
        }

        private void CleanupBlockedVehicles()
        {
            _removalBuffer.Clear();

            foreach (int handle in _blockedVehicles)
            {
                Vehicle vehicle = Entity.FromHandle(handle) as Vehicle;

                if (!ValidFunctions.IsValidVehicle(vehicle) || vehicle.IsDead)
                {
                    _removalBuffer.Add(handle);
                }
            }

            foreach (int handle in _removalBuffer)
            {
                _blockedVehicles.Remove(handle);
            }
        }

        public void BlockVehicle(int originalVehicleHandle)
        {
            if (originalVehicleHandle == -1)
            {
                return;
            }

            RemoveGhostGroup(originalVehicleHandle, true);
            _blockedVehicles.Add(originalVehicleHandle);
        }

        public void UnblockVehicle(int originalVehicleHandle)
        {
            if (originalVehicleHandle == -1)
            {
                return;
            }

            _blockedVehicles.Remove(originalVehicleHandle);
        }

        private void CleanupTemporaryDriverPeds(int now)
        {
            for (int i = _tempDriverPeds.Count - 1; i >= 0; i--)
            {
                var (ped, _, _, deleteTime) = _tempDriverPeds[i];

                // Wait until the 2-second delete time has passed
                if (deleteTime > now)
                {
                    continue;
                }

                if (ValidFunctions.IsValidPed(ped))
                {
                    ped.Delete();
                }

                _tempDriverPeds.RemoveAt(i);
            }
        }

        private static Vehicle CreateGhostVehicleFor(Vehicle originalVehicle)
        {
            if (!ValidFunctions.IsValidVehicle(originalVehicle))
            {
                return null;
            }

            Vector3 spawnPosition = originalVehicle.Position + new Vector3(0.0f, 0.0f, 2.0f);

            Vehicle ghostVehicle = Vehicle.Create(originalVehicle.Model, spawnPosition);

            if (!ValidFunctions.IsValidVehicle(ghostVehicle))
            {
                return null;
            }

            ghostVehicle.Heading = originalVehicle.Heading;
            ghostVehicle.IsEngineRunning = true;
            ghostVehicle.IsCollisionEnabled = false;
            ghostVehicle.CanBeVisiblyDamaged = false;
            ghostVehicle.DontLoadCollision = true;
            ghostVehicle.IsCollisionProof = true;
            ghostVehicle.IsInvincible = true;
            ghostVehicle.IsVisible = false;

            ghostVehicle.AttachTo(originalVehicle);

            return ghostVehicle;
        }

        private void AddStationSpam(int originalHandle, Vehicle vehicle, string station, int deleteTime)
        {
            RemoveStationSpam(originalHandle);

            if (!ValidFunctions.IsValidVehicle(vehicle) || string.IsNullOrWhiteSpace(station))
            {
                return;
            }

            _stationSpamEntries.Add((originalHandle, vehicle, station, deleteTime));

            // Apply once immediately.
            Function.Call(Hash.SET_VEH_RADIO_STATION, vehicle, station);
        }

        private void RemoveStationSpam(int originalHandle)
        {
            for (int i = _stationSpamEntries.Count - 1; i >= 0; i--)
            {
                if (_stationSpamEntries[i].originalHandle == originalHandle)
                {
                    _stationSpamEntries.RemoveAt(i);
                }
            }
        }

        private void CleanupStationSpam(int now)
        {
            for (int i = _stationSpamEntries.Count - 1; i >= 0; i--)
            {
                var (_, vehicle, _, deleteTime) = _stationSpamEntries[i];

                if (deleteTime <= now || !ValidFunctions.IsValidVehicle(vehicle))
                {
                    _stationSpamEntries.RemoveAt(i);
                }
            }
        }

        private void MaintainStationSpam()
        {
            for (int i = 0; i < _stationSpamEntries.Count; i++)
            {
                var (_, vehicle, station, _) = _stationSpamEntries[i];

                if (ValidFunctions.IsValidVehicle(vehicle) && !string.IsNullOrWhiteSpace(station))
                {
                    Function.Call(Hash.SET_VEH_RADIO_STATION, vehicle, station);
                }
            }
        }
    }
}