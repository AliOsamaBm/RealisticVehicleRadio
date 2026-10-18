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
using System.Drawing;
using System.Collections.Generic;

using static RealisticVehicleRadio.Helpers;
using static RealisticVehicleRadio.Configuration;

using GTA;
using GTA.Math;

namespace RealisticVehicleRadio
{
    public static class TunnelDebugVisualizer
    {
        public static bool EnableDebug { get; set; } = false;

        private static Vector3 _cachedProjectionPoint = Vector3.Zero;
        private static bool _hasCachedProjection = false;
        private static int _nextProjectionCalcTime = 0;

        // Draw the entire captured polyline and per-sample visuals
        public static void DrawCapturedTunnelDebug(Vector3 playerPos, IReadOnlyList<TunnelPoint> tunnelPoints, TunnelDebugVisualizerSettings settings, bool isLiveRecording = false)
        {
            if (tunnelPoints == null || tunnelPoints.Count == 0)
            {
                return;
            }

            float maxDist = settings.MaxDrawDistance;
            float maxDistSqr = maxDist * maxDist;

            // Use distinct colors for live recording so you can see how it aligns with saved tunnels!
            // Saved: Green/Yellow/Cyan | Live: Magenta/Orange/White
            Color segColor = isLiveRecording ? Color.FromArgb(200, 255, 0, 255) : Color.FromArgb(200, 0, 200, 0);
            Color widthColor = isLiveRecording ? Color.FromArgb(200, 255, 165, 0) : Color.FromArgb(200, 255, 200, 0);
            Color vertColor = isLiveRecording ? Color.FromArgb(200, 255, 255, 255) : Color.FromArgb(200, 0, 200, 200);

            // Draw centerline segments.
            for (int i = 0; i < tunnelPoints.Count - 1; i++)
            {
                TunnelPoint a = tunnelPoints[i];
                TunnelPoint b = tunnelPoints[i + 1];

                if ((a.Center - playerPos).LengthSquared() > maxDistSqr &&
                    (b.Center - playerPos).LengthSquared() > maxDistSqr)
                {
                    continue;
                }

                World.DrawLine(a.Center, b.Center, segColor);
            }

            // Draw each cross-section exactly once.
            for (int i = 0; i < tunnelPoints.Count; i++)
            {
                TunnelPoint point = tunnelPoints[i];

                if ((point.Center - playerPos).LengthSquared() > maxDistSqr)
                {
                    continue;
                }

                DrawSampleCross(point, widthColor, vertColor, playerPos, settings);
            }
        }

        // Draw width line (left-right) and vertical floor->ceiling at sample
        public static void DrawSampleCross(TunnelPoint p, Color widthColor, Color vertColor, Vector3 playerPos, TunnelDebugVisualizerSettings settings)
        {
            float maxDist = settings.MaxDrawDistance;
            float maxDistSqr = maxDist * maxDist;

            if ((p.Center - playerPos).LengthSquared() > maxDistSqr)
            {
                return;
            }

            // Draw the EXACT recorded physical walls
            Vector3 leftPt = p.LeftWall == Vector3.Zero ? new Vector3(p.Center.X, p.Center.Y, p.Center.Z) : new Vector3(p.LeftWall.X, p.LeftWall.Y, p.Center.Z + 0.05f);
            Vector3 rightPt = p.RightWall == Vector3.Zero ? new Vector3(p.Center.X, p.Center.Y, p.Center.Z) : new Vector3(p.RightWall.X, p.RightWall.Y, p.Center.Z + 0.05f);

            World.DrawLine(leftPt, rightPt, widthColor);

            // draw vertical floor->ceiling
            Vector3 floorP = new Vector3(p.Center.X, p.Center.Y, p.FloorZ);
            Vector3 ceilP = new Vector3(p.Center.X, p.Center.Y, p.CeilingZ);
            World.DrawLine(floorP, ceilP, vertColor);

            // small cross at center
            float crossSize = Math.Min(0.5f, p.Width * 0.15f);
            World.DrawLine(p.Center + new Vector3(crossSize, 0, 0), p.Center - new Vector3(crossSize, 0, 0), Color.FromArgb(200, 255, 255, 255));
            World.DrawLine(p.Center + new Vector3(0, crossSize, 0), p.Center - new Vector3(0, crossSize, 0), Color.FromArgb(200, 255, 255, 255));
        }
        public static void DrawSinglePointPreview(TunnelPoint? p, Vector3 playerPos, TunnelDebugVisualizerSettings settings)
        {
            if (!p.HasValue)
            {
                return;
            }

            TunnelPoint point = p.Value;

            // Use distinct colors for the live preview
            Color widthColor = Color.FromArgb(200, 255, 165, 0); // Orange
            Color vertColor = Color.FromArgb(200, 255, 255, 255); // White

            DrawSampleCross(point, widthColor, vertColor, playerPos, settings);

            // Draw a prominent 3D cross at the center to indicate exact point placement
            float crossSize = 1.5f;
            Color magenta = Color.FromArgb(255, 255, 0, 255);
            World.DrawLine(point.Center + new Vector3(crossSize, 0, 0), point.Center - new Vector3(crossSize, 0, 0), magenta);
            World.DrawLine(point.Center + new Vector3(0, crossSize, 0), point.Center - new Vector3(0, crossSize, 0), magenta);
            World.DrawLine(point.Center + new Vector3(0, 0, crossSize), point.Center - new Vector3(0, 0, crossSize), magenta);
        }

        // Draw the vehicle's projection (closest point on segment) for debugging (red)
        public static void DrawVehicleProjectionDebug(Vector3 vehiclePos, List<Tunnel> tunnels, int now)
        {
            // 1. Throttle the HEAVY MATH (every 100ms)
            if (now >= _nextProjectionCalcTime)
            {
                if (tunnels == null || tunnels.Count == 0)
                {
                    return;
                }

                _nextProjectionCalcTime = now + 100;
                _hasCachedProjection = false;

                // Find nearest segment across ALL tunnels
                float bestSqr = float.MaxValue;
                Vector3 bestClosest = Vector3.Zero;

                // 1. Iterate through every saved Tunnel
                foreach (Tunnel tunnel in tunnels)
                {
                    // Skip tunnels that don't have enough points to form a segment
                    if (tunnel.Points == null || tunnel.Points.Count < 2)
                    {
                        continue;
                    }

                    // 2. Iterate through the individual TunnelPoints inside this Tunnel
                    for (int i = 0; i < tunnel.Points.Count - 1; i++)
                    {
                        Vector3 A = tunnel.Points[i].Center;
                        Vector3 B = tunnel.Points[i + 1].Center;
                        Vector3 AB = B - A;
                        float abLen2 = AB.LengthSquared();
                        if (abLen2 <= 0.0001f)
                        {
                            continue;
                        }

                        float t = Vector3.Dot(vehiclePos - A, AB) / abLen2;
                        t = MathFunctions.Clamp(t, 0f, 1f);
                        Vector3 closest = A + AB * t;
                        float sq = (closest - vehiclePos).LengthSquared();

                        // Keep track of the absolute closest point found across all tunnels
                        if (sq < bestSqr)
                        {
                            bestSqr = sq;
                            bestClosest = closest;
                        }
                    }
                }

                if (bestSqr != float.MaxValue)
                {
                    _cachedProjectionPoint = bestClosest;
                    _hasCachedProjection = true;
                }
            }

            // 2. Draw EVERY FRAME using the cached result (prevents flickering)
            if (_hasCachedProjection)
            {
                Color red = Color.FromArgb(220, 200, 30, 30);
                World.DrawLine(vehiclePos, _cachedProjectionPoint, red);
                World.DrawLine(_cachedProjectionPoint + new Vector3(0.15f, 0, 0), _cachedProjectionPoint - new Vector3(0.15f, 0, 0), red);
                World.DrawLine(_cachedProjectionPoint + new Vector3(0, 0.15f, 0), _cachedProjectionPoint - new Vector3(0, 0.15f, 0), red);
            }
        }
        public static void DrawAllTunnels(Vector3 playerPos, List<Tunnel> tunnels, TunnelDebugVisualizerSettings settings)
        {
            if (tunnels == null || tunnels.Count == 0)
            {
                return;
            }

            foreach (var tunnel in tunnels)
            {
                if (tunnel.Points != null && tunnel.Points.Count > 0)
                {
                    // Draw each tunnel individually using the existing logic
                    DrawCapturedTunnelDebug(playerPos, tunnel.Points, settings, false);
                }
            }
        }
    }
}