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

using static RealisticVehicleRadio.Helpers;

using GTA;
using GTA.Math;

namespace RealisticVehicleRadio
{
    public sealed class PlayerTunnelEffect
    {
        public bool IsActive => _isActive;
        public bool ShouldMuteRadio { get; private set; }
        public float TargetSignalLevel { get; private set; } = 1f;

        private const float _PLAYER_TUNNEL_STATIC_MAX = 0.7f;
        private const float _PLAYER_TUNNEL_ENTRANCE_END = 1f / 3f;
        private const float _PLAYER_TUNNEL_EXIT_START = 2f / 3f;

        private enum TunnelSection
        {
            None,
            Entrance,
            Middle,
            Exit
        }

        private bool _isActive = false;

        public void Update(Ped playerPed, in VehicleEntry entry, Configuration.MainSettings mainCfg)
        {
            bool muteInTunnel = mainCfg.MuteRadioInTunnels;
            bool staticInTunnel = mainCfg.EnableStaticRadioInTunnels;

            if (!ValidFunctions.IsValidPed(playerPed) || !ValidFunctions.IsValidVehicle(entry.Vehicle))
            {
                RestoreFx();
                return;
            }

            if (playerPed.IsExitingVehicle || !entry.Vehicle.IsEngineRunning)
            {
                RestoreFx();
                return;
            }

            /* 
            S = false: Static is disabled.
            Restore the tunnel FX; any mute-only behavior is handled elsewhere.
            */
            if (!staticInTunnel)
            {
                RestoreFx();
                return;
            }

            if (!TryGetTunnelProgress(entry.Vehicle.Position, out float progress))
            {
                RestoreFx();
                return;
            }

            TunnelSection section = GetTunnelSection(progress);
            float signalLevel = GetStaticLevel(section, progress);

            TargetSignalLevel = signalLevel;

            ShouldMuteRadio = muteInTunnel && section == TunnelSection.Middle;

            _isActive = true;
        }

        public void RestoreFx()
        {
            ShouldMuteRadio = false;
            TargetSignalLevel = 1f;
            _isActive = false;
            ShouldMuteRadio = false;
        }

        private bool TryGetTunnelProgress(Vector3 position, out float progress)
        {
            progress = 0f;

            // 1. Broadphase & Exact containment in ONE pass
            if (!TunnelSpatialIndex.TryGetContainingSegment(position, out int segIdx))
            {
                return false;
            }

            var segments = TunnelSpatialIndex.Segments;
            if (segIdx < 0 || segIdx >= segments.Count)
            {
                return false;
            }

            TunnelSpatialIndex.Segment seg = segments[segIdx];

            // 2. O(1) Tunnel Lookup (using the Dictionary cache)
            Tunnel bestTunnel = TunnelManager.GetTunnelById(seg.TunnelId);
            if (bestTunnel == null || bestTunnel.Points == null)
            {
                return false;
            }

            int count = bestTunnel.Points.Count;
            if (count <= 1)
            {
                progress = 0.5f;
                return true;
            }

            // 3. Refine progress using the exact segment we are currently inside
            int idxA = seg.PointIndexA;
            int idxB = Math.Min(idxA + 1, count - 1);

            Vector3 pA = bestTunnel.Points[idxA].Center;
            Vector3 pB = bestTunnel.Points[idxB].Center;

            Vector3 ab = pB - pA;
            float lenSq = ab.LengthSquared();

            float t = 0f;
            if (lenSq > 0.0001f)
            {
                t = Vector3.Dot(position - pA, ab) / lenSq;
                t = MathFunctions.Clamp(t, 0f, 1f);
            }

            progress = (idxA + t) / (count - 1);

            return true;
        }

        private TunnelSection GetTunnelSection(float progress)
        {
            if (progress <= _PLAYER_TUNNEL_ENTRANCE_END)
            {
                return TunnelSection.Entrance;
            }

            if (progress >= _PLAYER_TUNNEL_EXIT_START)
            {
                return TunnelSection.Exit;
            }

            return TunnelSection.Middle;
        }

        private float GetStaticLevel(TunnelSection section, float progress)
        {
            if (section == TunnelSection.Entrance)
            {
                float t = MathFunctions.Clamp(progress / _PLAYER_TUNNEL_ENTRANCE_END, 0f, 1f);

                // 0.7 at entrance -> 0.0 at middle
                return _PLAYER_TUNNEL_STATIC_MAX * (1f - t);
            }

            if (section == TunnelSection.Exit)
            {
                float t = MathFunctions.Clamp((progress - _PLAYER_TUNNEL_EXIT_START) / (1f - _PLAYER_TUNNEL_EXIT_START), 0f, 1f);

                // 0.0 at middle -> 0.7 at exit
                return _PLAYER_TUNNEL_STATIC_MAX * t;
            }

            // Middle
            return 0f;
        }
    }
}