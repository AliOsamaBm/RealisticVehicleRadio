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
using System.Collections.Generic;

using static RealisticVehicleRadio.Helpers;
using static RealisticVehicleRadio.Configuration;

using GTA;
using GTA.Math;

namespace RealisticVehicleRadio
{
    public static class TunnelRecorder
    {
        // ==========================================
        // MODE STATE
        // ==========================================

        private static bool _enableTunnelRecorder = false;
        private static bool _enableManualRecording = false;
        private static bool _recording = false;

        public static bool EnableTunnelRecorder
        {
            get => _enableTunnelRecorder;
            set
            {
                if (_enableTunnelRecorder == value)
                {
                    return;
                }

                // Prevent automatic recording from being enabled while manual recording is active.
                if (value && _enableManualRecording)
                {
                    GUI.ShowContextualMessage("~r~Disable manual recording before using automatic recording.");
                    return;
                }

                _enableTunnelRecorder = value;
            }
        }

        // Manual recording is now controlled through TrySetManualRecording().
        public static bool EnableManualRecording => _enableManualRecording;

        public static string PendingTunnelName { get; set; } = "New Tunnel";

        // ==========================================
        // SAMPLE STORAGE
        // ==========================================

        // Automatic drive-recording samples.
        private static readonly List<TunnelSample> _autoSamples = new List<TunnelSample>();

        // Manual recording samples.
        private static readonly List<TunnelSample> _manualSamples = new List<TunnelSample>();

        // Automatic recording last-state tracking.
        private static Vector3 _lastAutoSamplePos = Vector3.Zero;
        private static Vector3 _lastAutoForward = Vector3.Zero;

        // ==========================================
        // PUBLIC COUNTER
        // ==========================================

        public static int LastSavedAutomaticSampleCount { get; private set; } = 0;

        // Exposes the live, unsaved manual point count for the OnTick safety net
        public static int ManualPointCount => _manualSamples?.Count ?? 0;

        // ==========================================
        // MANUAL MODE CONTROL
        // ==========================================

        public static bool TrySetManualRecording(bool enabled)
        {
            if (_enableManualRecording == enabled)
            {
                return true;
            }

            // Prevent manual recording from being enabled while automatic recording is active.
            if (enabled && (_enableTunnelRecorder || _recording))
            {
                GUI.ShowContextualMessage("~r~Stop automatic tunnel recording before enabling manual recording.");
                return false;
            }

            _enableManualRecording = enabled;

            // Intentionally do NOT clear _manualSamples here.
            // This prevents accidental loss of unsaved manual points if the checkbox is toggled.
            // Use ClearManualPoints() or the new Clear Manual Points menu button to discard them.

            return true;
        }

        // ==========================================
        // AUTOMATIC RECORDING
        // ==========================================

        public static void Record(Ped ped, Vehicle vehicle, Vector3 position, TunnelRecorderSettings settings)
        {
            try
            {
                if (!_recording || _enableManualRecording)
                {
                    return;
                }

                float minDistance = settings.MinDistanceMeters;
                float headingThreshold = settings.HeadingDeltaThresholdDeg;

                if (minDistance <= 0f && headingThreshold <= 0f)
                {
                    return;
                }

                bool distanceTrigger = minDistance > 0f;
                bool headingTrigger = headingThreshold > 0f;

                Vector3 currentForward = vehicle.ForwardVector;
                Vector3 currentPos = position;

                // Sample when the vehicle has moved sufficiently OR heading changed enough.
                float dist = 0f;
                if (distanceTrigger)
                {
                    dist = _lastAutoSamplePos == Vector3.Zero
                     ? float.MaxValue
                     : currentPos.DistanceTo(_lastAutoSamplePos);
                }

                float headingDelta = 0f;
                if (headingTrigger)
                {
                    if (_lastAutoForward == Vector3.Zero)
                    {
                        headingDelta = float.MaxValue;
                    }
                    else
                    {
                        float dot = MathFunctions.Clamp(Vector3.Dot(currentForward, _lastAutoForward), -1f, 1f);
                        float angleRad = (float)Math.Acos(dot);
                        headingDelta = angleRad * (180f / (float)Math.PI);
                    }
                }

                bool shouldSample = (distanceTrigger && dist >= minDistance) || (headingTrigger && headingDelta >= headingThreshold);

                if (shouldSample)
                {

                    // NEW in V1.3.0: Remove peds and vehicles automatically while recording
                    EnsureNoInterferingPedsAndVehicles(ped, vehicle, position, settings);

                    TunnelSample currentSample = SampleAtVehicle(vehicle, settings);

                    // =====================================================
                    // HIGH-SPEED INTERPOLATION (Fixes gaps at high speeds)
                    // =====================================================
                    if (_autoSamples.Count > 0)
                    {
                        TunnelSample prevSample = _autoSamples[_autoSamples.Count - 1];

                        /*
                        Compute the number of sub-segments to place between prevSample
                        and currentSample.
                        
                        The script runs once per frame, so at high speed the vehicle can
                        move many metres in a single tick. To keep point spacing near
                        MinDistanceMeters we must interpolate the missing points.
                        
                        IMPORTANT: Do the division in double. If MinDistanceMeters is
                        extremely small (e.g. float.Epsilon) the result overflows int,
                        and a float->int cast of an out-of-range value is UNDEFINED in
                        C# (on x64 it yields int.MinValue). That silently kills the
                        interpolation loop below.
                        
                        The per-frame cap is a safety valve: anything the cap truncates
                        simply means that frame's spacing becomes dist/cap instead of
                        MinDistanceMeters. This is the unavoidable trade-off of a
                        frame-driven sampler.
                        */

                        const int MAX_INTERPOLATIONS_PER_FRAME = 100;

                        double rawSegments = dist / (double)minDistance;

                        int segments;
                        if (double.IsNaN(rawSegments) || rawSegments <= 1.0)
                        {
                            segments = 1;
                        }
                        else if (rawSegments >= MAX_INTERPOLATIONS_PER_FRAME)
                        {
                            segments = MAX_INTERPOLATIONS_PER_FRAME;
                        }
                        else
                        {
                            segments = (int)Math.Ceiling(rawSegments);
                        }

                        for (int i = 1; i < segments; i++)
                        {
                            float t = (float)i / segments;

                            float deltaHeading = currentSample.Heading - prevSample.Heading;
                            if (deltaHeading > 180f)
                            {
                                deltaHeading -= 360f;
                            }

                            if (deltaHeading < -180f)
                            {
                                deltaHeading += 360f;
                            }

                            float interpHeading = prevSample.Heading + deltaHeading * t;
                            if (interpHeading < 0f)
                            {
                                interpHeading += 360f;
                            }

                            if (interpHeading >= 360f)
                            {
                                interpHeading -= 360f;
                            }

                            TunnelSample interp = new TunnelSample
                            {
                                Center = prevSample.Center + (currentSample.Center - prevSample.Center) * t,
                                LeftWall = prevSample.LeftWall + (currentSample.LeftWall - prevSample.LeftWall) * t,
                                RightWall = prevSample.RightWall + (currentSample.RightWall - prevSample.RightWall) * t,
                                FloorZ = prevSample.FloorZ + (currentSample.FloorZ - prevSample.FloorZ) * t,
                                CeilingZ = prevSample.CeilingZ + (currentSample.CeilingZ - prevSample.CeilingZ) * t,
                                Width = prevSample.Width + (currentSample.Width - prevSample.Width) * t,
                                Heading = interpHeading,
                                Timestamp = DateTime.UtcNow
                            };
                            _autoSamples.Add(interp);
                        }
                    }

                    _autoSamples.Add(currentSample);
                    _lastAutoSamplePos = currentPos;
                    _lastAutoForward = currentForward;

                    GUI.ShowContextualMessage(
                        $"~b~Tunnel Recorder:~b~ ~w~samples = ~y~{_autoSamples.Count}~w~ " +
                        $"(w=~y~{currentSample.Width:F2}m~w~ h=~y~{currentSample.CeilingZ - currentSample.FloorZ:F2}m~w~)",
                        -1
                    );
                }
            }
            catch (Exception ex)
            {
                Logger.DisplayAndLogError(ex, "~r~Tunnel Recorder: ERROR. Check log file.");
            }
        }

        public static void HandleToggleKey(bool vehicleLost = false)
        {
            // Hard guard: automatic recording toggle should not operate while manual mode is active
            if (_enableManualRecording)
            {
                GUI.ShowContextualMessage("~r~Exit manual mode first.", 3000);
                return;
            }

            _recording = !_recording;

            if (_recording)
            {
                // Start a new automatic session
                _autoSamples.Clear();
                _lastAutoSamplePos = Vector3.Zero;
                _lastAutoForward = Vector3.Zero;
                LastSavedAutomaticSampleCount = 0;
            }
            else
            {
                // Stop automatic session
                LastSavedAutomaticSampleCount = _autoSamples.Count;

                if (_autoSamples.Count > 1)
                {
                    try
                    {
                        TunnelPoint[] tunnelPoints = ConvertSamplesToTunnelPoints(_autoSamples);
                        TunnelManager.AddTunnel(tunnelPoints, PendingTunnelName);

                        // Clear the automatic session after saving
                        _autoSamples.Clear();
                        _lastAutoSamplePos = Vector3.Zero;
                        _lastAutoForward = Vector3.Zero;

                        string prefix = vehicleLost ? "~o~Vehicle lost. " : "~g~";
                        GUI.ShowContextualMessage($"{prefix}Saved {LastSavedAutomaticSampleCount} pts.", 3000);
                    }
                    catch (Exception ex)
                    {
                        Logger.DisplayAndLogError(ex, "Failed to save recorded tunnel.");
                        GUI.ShowContextualMessage("~r~Save failed.", 3000);
                    }
                }
                else
                {
                    string prefix = vehicleLost ? "~o~Vehicle lost. " : "~r~";
                    GUI.ShowContextualMessage($"{prefix}Need 2+ points.", 3000);
                    _autoSamples.Clear();
                }
            }
        }



        public static TunnelPoint[] GetLiveRecording()
        {
            if (_autoSamples == null || _autoSamples.Count == 0)
            {
                return Array.Empty<TunnelPoint>();
            }

            return ConvertSamplesToTunnelPoints(_autoSamples);
        }

        // ==========================================
        // MANUAL RECORDING
        // ==========================================

        public static void AddManualPoint(Vehicle veh, TunnelRecorderSettings settings)
        {
            if (!_enableManualRecording)
            {
                GUI.ShowContextualMessage("~b~Manual Recording:~b~ ~r~Enable manual mode first.", 3000);
                return;
            }

            if (_enableTunnelRecorder || _recording)
            {
                GUI.ShowContextualMessage("~b~Manual Recording:~b~ ~r~Stop auto recording first.", 3000);
                return;
            }

            if (!ValidFunctions.IsValidVehicle(veh))
            {
                GUI.ShowContextualMessage("~b~Manual Recording:~b~ ~r~Enter a vehicle to add point.", 3000);
                return;
            }

            TunnelSample s = SampleAtVehicle(veh, settings);
            _manualSamples.Add(s);
            GUI.ShowContextualMessage($"~b~Manual Recording:~b~ ~g~Point added. ({_manualSamples.Count} total)", 2000);
        }

        public static void DeletePreviousPoint()
        {
            if (_manualSamples.Count > 0)
            {
                _manualSamples.RemoveAt(_manualSamples.Count - 1);
                GUI.ShowContextualMessage($"~y~Deleted previous manual point. Remaining: {_manualSamples.Count}");
            }
            else
            {
                GUI.ShowContextualMessage("~r~No manual points to delete.");
            }
        }

        public static void ClearManualPoints(bool vehicleLost = false)
        {
            int count = _manualSamples.Count;

            if (count == 0)
            {
                GUI.ShowContextualMessage("~y~No points to clear.", 2000);
                return;
            }

            _manualSamples.Clear();

            string prefix = vehicleLost ? "~o~Vehicle lost. " : "~r~";
            GUI.ShowContextualMessage($"~b~Manual Recording:~b~ {prefix}Cleared {count} pts.", 3000);
        }

        public static void SaveManualChanges(bool vehicleLost = false)
        {
            const int MINIMUM_MANUAL_POINTS = 2;

            if (_manualSamples.Count < MINIMUM_MANUAL_POINTS)
            {
                string prefix = vehicleLost ? "~o~Vehicle lost. " : "~r~";
                GUI.ShowContextualMessage($"~b~Manual Recording:~b~ {prefix}Need 2+ points.", 3000);
                return;
            }

            try
            {
                TunnelPoint[] tunnelPoints = ConvertSamplesToTunnelPoints(_manualSamples);
                TunnelManager.AddTunnel(tunnelPoints, PendingTunnelName);

                int count = _manualSamples.Count;
                _manualSamples.Clear();

                string prefix = vehicleLost ? "~o~Vehicle lost. " : "~g~";
                GUI.ShowContextualMessage($"~b~Manual Recording:~b~ {prefix}Saved {count} pts.", 3000);
            }
            catch (Exception ex)
            {
                Logger.DisplayAndLogError(ex, "Failed to save manual tunnel.");
                GUI.ShowContextualMessage("~b~Manual Recording:~b~ ~r~Save failed.", 3000);
            }
        }

        // ==========================================
        // PREVIEW / DEBUG / UTILITIES
        // ==========================================

        public static TunnelPoint? ComputePreviewPoint(Vehicle veh, TunnelRecorderSettings settings)
        {
            if (veh == null || !veh.Exists())
            {
                return null;
            }

            TunnelSample s = SampleAtVehicle(veh, settings);

            return new TunnelPoint(
                s.Center,
                s.LeftWall,
                s.RightWall,
                s.FloorZ,
                s.CeilingZ,
                s.Width,
                s.Heading
            );
        }

        // ==========================================
        // INTERNAL HELPERS
        // ==========================================

        private static TunnelPoint[] ConvertSamplesToTunnelPoints(List<TunnelSample> samples)
        {
            TunnelPoint[] tunnelPoints = new TunnelPoint[samples.Count];

            for (int i = 0; i < samples.Count; i++)
            {
                TunnelSample sample = samples[i];

                tunnelPoints[i] = new TunnelPoint(
                    sample.Center,
                    sample.LeftWall,
                    sample.RightWall,
                    sample.FloorZ,
                    sample.CeilingZ,
                    sample.Width,
                    sample.Heading
                );
            }

            return tunnelPoints;
        }

        private static TunnelSample SampleAtVehicle(Vehicle veh, TunnelRecorderSettings settings)
        {
            float maxHorizontalSearch = settings.MaxHorizontalSearch;
            float maxVerticalSearch = settings.MaxVerticalSearch;

            Vector3 vehPos = veh.Position;
            Vector3 start = new Vector3(vehPos.X, vehPos.Y, vehPos.Z + settings.WallRayHeightOffset);

            Vector3 right = veh.RightVector;
            Vector3 forward = veh.ForwardVector;
            Vector3 up = veh.UpVector;

            Vector3 leftTarget = start - right * maxHorizontalSearch;
            Vector3 rightTarget = start + right * maxHorizontalSearch;

            RaycastResult leftRes = World.Raycast(start, leftTarget, IntersectFlags.Everything, veh);
            RaycastResult rightRes = World.Raycast(start, rightTarget, IntersectFlags.Everything, veh);

            // Store actual hits, or max search distance if missed.
            Vector3 leftHitPos = leftRes.DidHit ? leftRes.HitPosition : leftTarget;
            Vector3 rightHitPos = rightRes.DidHit ? rightRes.HitPosition : rightTarget;

            // Floor and ceiling.
            Vector3 upTarget = start + up * maxVerticalSearch;
            Vector3 downTarget = start - up * maxVerticalSearch;

            RaycastResult upRes = World.Raycast(start, upTarget, IntersectFlags.Everything, veh);
            RaycastResult downRes = World.Raycast(start, downTarget, IntersectFlags.Everything, veh);

            float ceilingZ = upRes.DidHit ? upRes.HitPosition.Z : upTarget.Z;
            float floorZ = downRes.DidHit ? downRes.HitPosition.Z : downTarget.Z;

            Vector3 trueCenter = (leftHitPos + rightHitPos) / 2f;
            float width = leftHitPos.DistanceTo(rightHitPos);
            float headingDeg = (float)(Math.Atan2(forward.Y, forward.X) * 180.0 / Math.PI);

            return new TunnelSample()
            {
                Center = trueCenter,
                LeftWall = leftHitPos,
                RightWall = rightHitPos,
                FloorZ = floorZ,
                CeilingZ = ceilingZ,
                Width = width,
                Heading = headingDeg,
                Timestamp = DateTime.UtcNow
            };
        }

        // New in V1.3.0: Delete all peds and vehicles automatically during recording
        public static void EnsureNoInterferingPedsAndVehicles(Ped ped, Vehicle vehicle, Vector3 position, TunnelRecorderSettings settings)
        {
            float maxHorizontalSearch = settings.MaxHorizontalSearch;
            float maxVerticalSearch = settings.MaxVerticalSearch;

            Vehicle[] interferingVehicles = World.GetNearbyVehicles(
                position,
                Math.Max(maxHorizontalSearch, maxVerticalSearch));

            for (int v = 0; v < interferingVehicles.Length; v++)
            {
                Vehicle veh = interferingVehicles[v];

                // CRITICAL SAFETY CHECK: Never delete the player's current vehicle!
                if (ValidFunctions.IsValidVehicle(veh) && veh != vehicle)
                {
                    veh.Delete();
                }
            }

            Ped[] interferingPeds = World.GetNearbyPeds(position, Math.Max(maxHorizontalSearch, maxVerticalSearch));

            for (int p = 0; p < interferingPeds.Length; p++)
            {
                Ped npc = interferingPeds[p];

                // CRITICAL SAFETY CHECK: Never delete the player
                if (ValidFunctions.IsValidPed(npc) && npc != ped)
                {
                    npc.Delete();
                }
            }
        }

        // ==========================================
        // DATA CONTAINER
        // ==========================================

        private class TunnelSample
        {
            public Vector3 Center;
            public Vector3 LeftWall;
            public Vector3 RightWall;
            public float FloorZ;
            public float CeilingZ;
            public float Width;
            public float Heading;
            public DateTime Timestamp;
        }
    }
}