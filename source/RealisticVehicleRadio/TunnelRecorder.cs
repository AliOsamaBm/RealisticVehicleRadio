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
using System.Collections.Generic;

using static Helpers;
using static Configuration;
using static RealisticVehicleRadio;

using GTA;
using GTA.UI;
using GTA.Math;

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
                Notification.PostTicker("~r~Disable manual recording before using automatic recording.", true);
                return;
            }

            _enableTunnelRecorder = value;
        }
    }

    // Manual recording is now controlled through TrySetManualRecording().
    public static bool EnableManualRecording => _enableManualRecording;

    public static bool IsAutomaticRecordingActive => _recording;

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
            Notification.PostTicker("~r~Stop automatic tunnel recording before enabling manual recording.", true);
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

    public static void Record(Vehicle vehicle, Vector3 position, TunnelRecorderSettings settings)
    {
        try
        {
            if (!_recording || _enableManualRecording)
            {
                return;
            }

            // Sample when the vehicle has moved sufficiently OR heading changed enough.
            Vector3 currentPos = position;
            Vector3 currentForward = vehicle.ForwardVector;

            float dist = _lastAutoSamplePos == Vector3.Zero
                ? float.MaxValue
                : currentPos.DistanceTo(_lastAutoSamplePos);

            float headingDelta = 0f;

            if (_lastAutoForward == Vector3.Zero)
            {
                headingDelta = float.MaxValue;
            }
            else
            {
                float dot = Math.Max(-1f, Math.Min(1f, Vector3.Dot(currentForward, _lastAutoForward)));
                float angleRad = (float)Math.Acos(dot);
                headingDelta = angleRad * (180f / (float)Math.PI);
            }

            bool shouldSample = dist >= settings.MinDistanceMeters || headingDelta >= settings.HeadingDeltaThresholdDeg;

            if (shouldSample)
            {
                TunnelSample s = SampleAtVehicle(vehicle, settings);

                _autoSamples.Add(s);

                _lastAutoSamplePos = currentPos;
                _lastAutoForward = currentForward;

                Screen.ShowHelpText(
                    $"~b~Tunnel Recorder:~b~ ~w~samples = ~y~{_autoSamples.Count}~w~ " +
                    $"(w=~y~{s.Width:F2}m~w~ h=~y~{s.CeilingZ - s.FloorZ:F2}m~w~)",
                    -1,
                    false
                );
            }
        }
        catch (Exception ex)
        {
            Logger.DisplayAndLogError(ex, "~r~Tunnel Recorder: ERROR. Check log file.");
        }
    }

    public static void HandleToggleKey()
    {
        // Hard guard: automatic recording toggle should not operate while manual mode is active.
        if (_enableManualRecording)
        {
            Notification.PostTicker("~r~Manual recording is active. Disable it before using automatic recording.", true);
            return;
        }

        _recording = !_recording;

        if (_recording)
        {
            // Start a new automatic session.
            _autoSamples.Clear();
            _lastAutoSamplePos = Vector3.Zero;
            _lastAutoForward = Vector3.Zero;
            LastSavedAutomaticSampleCount = 0;
        }
        else
        {
            // Stop automatic session.
            LastSavedAutomaticSampleCount = _autoSamples.Count;

            if (_autoSamples.Count > 1)
            {
                try
                {
                    TunnelPoint[] tunnelPoints = ConvertSamplesToTunnelPoints(_autoSamples);

                    TunnelManager.AddTunnel(tunnelPoints, PendingTunnelName);

                    // Clear the automatic session after saving.
                    _autoSamples.Clear();
                    _lastAutoSamplePos = Vector3.Zero;
                    _lastAutoForward = Vector3.Zero;
                }
                catch (Exception ex)
                {
                    Logger.DisplayAndLogError(ex, "Failed to save recorded tunnel.");
                }
            }
            else
            {
                Notification.PostTicker($"~r~A tunnel needs at least 2 points.", true);
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

    public static TunnelPoint[] GetLiveManualRecording()
    {
        if (_manualSamples == null || _manualSamples.Count == 0)
        {
            return Array.Empty<TunnelPoint>();
        }

        return ConvertSamplesToTunnelPoints(_manualSamples);
    }

    // ==========================================
    // MANUAL RECORDING
    // ==========================================

    public static void AddManualPoint(Vehicle veh, TunnelRecorderSettings settings)
    {
        if (!_enableManualRecording)
        {
            Notification.PostTicker("~r~Enable manual recording before adding manual points.", true);
            return;
        }

        if (_enableTunnelRecorder || _recording)
        {
            Notification.PostTicker("~r~Stop automatic recording before adding manual points.", true);
            return;
        }

        if (veh == null || !veh.Exists())
        {
            Notification.PostTicker("~r~You must be in a vehicle to add a manual point.", true);
            return;
        }

        TunnelSample s = SampleAtVehicle(veh, settings);

        _manualSamples.Add(s);

        Notification.PostTicker($"~g~Added manual point. Total manual points: {_manualSamples.Count}", true);
    }

    public static void DeletePreviousPoint()
    {
        if (_manualSamples.Count > 0)
        {
            _manualSamples.RemoveAt(_manualSamples.Count - 1);
            Notification.PostTicker($"~y~Deleted previous manual point. Remaining: {_manualSamples.Count}", true);
        }
        else
        {
            Notification.PostTicker("~r~No manual points to delete.", true);
        }
    }

    public static void ClearManualPoints()
    {
        int count = _manualSamples.Count;

        if (count == 0)
        {
            Notification.PostTicker("~y~No manual points to clear.", true);
            return;
        }

        _manualSamples.Clear();

        Notification.PostTicker($"~r~Cleared {count} manual point(s).", true);
    }

    public static void SaveManualChanges()
    {
        const int minimumManualPoints = 2;

        if (_manualSamples.Count < minimumManualPoints)
        {
            Notification.PostTicker($"~r~A tunnel needs at least {minimumManualPoints} points.", true);
            return;
        }

        try
        {
            TunnelPoint[] tunnelPoints = ConvertSamplesToTunnelPoints(_manualSamples);

            TunnelManager.AddTunnel(tunnelPoints, PendingTunnelName);

            PendingTunnelName = "New Tunnel";

            _manualSamples.Clear();

            Notification.PostTicker("~g~Manual tunnel saved and manual session cleared.", true);
        }
        catch (Exception ex)
        {
            Logger.DisplayAndLogError(ex, "Failed to save manual tunnel.");
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

    public static void DeleteNearestTunnel(Vector3 playerPos)
    {
        if (TunnelManager.Tunnels.Count == 0)
        {
            return;
        }

        int closestTunnelId = -1;
        float minDist = float.MaxValue;

        foreach (Tunnel tunnel in TunnelManager.Tunnels)
        {
            foreach (var point in tunnel.Points)
            {
                float dist = point.Center.DistanceToSquared(playerPos);

                if (dist < minDist)
                {
                    minDist = dist;
                    closestTunnelId = tunnel.Id;
                }
            }
        }

        if (closestTunnelId != -1)
        {
            TunnelManager.DeleteTunnel(closestTunnelId);
            Screen.ShowHelpText($"~r~Deleted Nearest Tunnel (#{closestTunnelId})", 3000, false);
        }
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