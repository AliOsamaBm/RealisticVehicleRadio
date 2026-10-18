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

using GTA.Math;

namespace RealisticVehicleRadio
{
    public static class TunnelSpatialIndex
    {
        public struct Segment
        {
            // New in V1.3.0: we can map the spatial index back to the actual tunnel data.
            public int TunnelId;
            public int PointIndexA; // The index of the first point of this segment

            public Vector2 AL;
            public Vector2 BL;
            public Vector2 BR;
            public Vector2 AR;

            public float MinX;
            public float MaxX;
            public float MinY;
            public float MaxY;

            public float MinZ;
            public float MaxZ;

            public Vector2 CenterA;
            public Vector2 CenterB;
            public float FloorZ_A;
            public float FloorZ_B;
            public float CeilZ_A;
            public float CeilZ_B;
        }

        private struct TunnelBounds
        {
            public float MinX;
            public float MaxX;
            public float MinY;
            public float MaxY;
        }

        // 64 meters is a good default for vehicle radio detection
        // If you have very dense city tunnels, you can try 32
        // If you have very long open highways, you can try 128
        private const float _CELL_SIZE = 64f;

        private static readonly List<Segment> _segments = new List<Segment>();
        private static readonly Dictionary<long, List<int>> _cells = new Dictionary<long, List<int>>();

        private static int[] _lastSeen = Array.Empty<int>();
        private static int _queryStamp = 1;

        private static float _builtHTolerance;
        private static float _builtVTolerance;

        public static bool IsDirty = true;

        public static int SegmentCount => _segments.Count;
        public static IReadOnlyList<Segment> Segments => _segments;

        public static void MarkDirty()
        {
            IsDirty = true;
        }

        public static void EnsureBuilt(List<Tunnel> tunnels, float hTolerance, float vTolerance)
        {
            if (!IsDirty && _builtHTolerance == hTolerance && _builtVTolerance == vTolerance)
            {
                return;
            }

            Rebuild(tunnels, hTolerance, vTolerance);
        }

        public static void Rebuild(List<Tunnel> tunnels, float hTolerance, float vTolerance)
        {
            _segments.Clear();
            _cells.Clear();

            _builtHTolerance = hTolerance;
            _builtVTolerance = vTolerance;
            IsDirty = false;

            if (tunnels == null)
            {
                _lastSeen = Array.Empty<int>();
                _queryStamp = 1;
                return;
            }

            for (int t = 0; t < tunnels.Count; t++)
            {
                Tunnel tunnel = tunnels[t];

                if (tunnel == null || tunnel.Points == null || tunnel.Points.Count < 2)
                {
                    continue;
                }

                List<TunnelPoint> points = tunnel.Points;

                bool hasBounds = false;
                TunnelBounds bounds = new TunnelBounds();

                for (int i = 0; i < points.Count - 1; i++)
                {
                    TunnelPoint a = points[i];
                    TunnelPoint b = points[i + 1];

                    Vector3 aLeft3 = !IsValidWall(a.LeftWall) ? EstimateWall(a, -1) : a.LeftWall;
                    Vector3 aRight3 = !IsValidWall(a.RightWall) ? EstimateWall(a, 1) : a.RightWall;
                    Vector3 bLeft3 = !IsValidWall(b.LeftWall) ? EstimateWall(b, -1) : b.LeftWall;
                    Vector3 bRight3 = !IsValidWall(b.RightWall) ? EstimateWall(b, 1) : b.RightWall;

                    Vector2 AL = new Vector2(aLeft3.X, aLeft3.Y);
                    Vector2 AR = new Vector2(aRight3.X, aRight3.Y);
                    Vector2 BL = new Vector2(bLeft3.X, bLeft3.Y);
                    Vector2 BR = new Vector2(bRight3.X, bRight3.Y);

                    Vector2 centerA = new Vector2(a.Center.X, a.Center.Y);
                    Vector2 centerB = new Vector2(b.Center.X, b.Center.Y);

                    if (hTolerance > 0f)
                    {
                        AL = ExpandAwayFromCenter(AL, centerA, hTolerance);
                        AR = ExpandAwayFromCenter(AR, centerA, hTolerance);
                        BL = ExpandAwayFromCenter(BL, centerB, hTolerance);
                        BR = ExpandAwayFromCenter(BR, centerB, hTolerance);
                    }

                    float minX = Min4(AL.X, BL.X, BR.X, AR.X);
                    float maxX = Max4(AL.X, BL.X, BR.X, AR.X);
                    float minY = Min4(AL.Y, BL.Y, BR.Y, AR.Y);
                    float maxY = Max4(AL.Y, BL.Y, BR.Y, AR.Y);

                    float minZ = Math.Min(a.FloorZ, b.FloorZ) - vTolerance;
                    float maxZ = Math.Max(a.CeilingZ, b.CeilingZ) + vTolerance;

                    Segment segment = new Segment
                    {
                        TunnelId = tunnel.Id,
                        PointIndexA = i,

                        AL = AL,
                        BL = BL,
                        BR = BR,
                        AR = AR,

                        MinX = minX,
                        MaxX = maxX,
                        MinY = minY,
                        MaxY = maxY,

                        MinZ = minZ,
                        MaxZ = maxZ,

                        CenterA = centerA,
                        CenterB = centerB,
                        FloorZ_A = a.FloorZ,
                        FloorZ_B = b.FloorZ,
                        CeilZ_A = a.CeilingZ,
                        CeilZ_B = b.CeilingZ
                    };

                    int segmentIndex = _segments.Count;
                    _segments.Add(segment);

                    AddToCells(minX, maxX, minY, maxY, segmentIndex);

                    if (!hasBounds)
                    {
                        bounds.MinX = minX;
                        bounds.MaxX = maxX;
                        bounds.MinY = minY;
                        bounds.MaxY = maxY;
                        hasBounds = true;
                    }
                    else
                    {
                        if (minX < bounds.MinX) bounds.MinX = minX;
                        if (maxX > bounds.MaxX) bounds.MaxX = maxX;
                        if (minY < bounds.MinY) bounds.MinY = minY;
                        if (maxY > bounds.MaxY) bounds.MaxY = maxY;
                    }
                }
            }

            _lastSeen = new int[_segments.Count];
            _queryStamp = 1;
        }

        /// <summary>
        /// Checks if the position is inside any tunnel segment.
        /// If true, outputs the exact index of the segment containing the position.
        /// This combines the broadphase (bounding box) and narrowphase (PointInQuad)
        /// checks into a single pass, eliminating the need for a separate
        /// GetNearbySegmentIndices query when you already know you are inside.
        /// </summary>
        public static bool TryGetContainingSegment(Vector3 position, out int segmentIndex)
        {
            segmentIndex = -1;

            if (_segments.Count == 0)
            {
                return false;
            }

            NextQueryStamp();

            int cx = CellCoord(position.X);
            int cy = CellCoord(position.Y);

            Vector2 p = new Vector2(position.X, position.Y);

            // Check 3x3 nearby cells to avoid edge misses.
            for (int ox = -1; ox <= 1; ox++)
            {
                for (int oy = -1; oy <= 1; oy++)
                {
                    long key = GetKey(cx + ox, cy + oy);

                    if (!_cells.TryGetValue(key, out List<int> cellSegments))
                    {
                        continue;
                    }

                    for (int i = 0; i < cellSegments.Count; i++)
                    {
                        int segIdx = cellSegments[i];

                        // Prevent duplicate checks if a segment spans multiple cells
                        if (_lastSeen[segIdx] == _queryStamp)
                        {
                            continue;
                        }

                        _lastSeen[segIdx] = _queryStamp;

                        Segment s = _segments[segIdx];

                        // 1. Broadphase: 3D Bounding Box Check
                        if (position.X < s.MinX || position.X > s.MaxX ||
                            position.Y < s.MinY || position.Y > s.MaxY ||
                            position.Z < s.MinZ || position.Z > s.MaxZ)
                        {
                            continue;
                        }

                        // 2. Narrowphase: Exact 2D Quad Containment
                        if (PointInQuad(p, s.AL, s.BL, s.BR, s.AR))
                        {
                            // 3. Narrowphase Z: Interpolate based on 2D projection
                            Vector2 ab = s.CenterB - s.CenterA;
                            float lenSq = ab.LengthSquared();
                            float t = 0f;
                            if (lenSq > 0.0001f)
                            {
                                t = Vector2.Dot(p - s.CenterA, ab) / lenSq;
                                t = MathFunctions.Clamp(t, 0f, 1f);
                            }

                            float exactFloorZ = s.FloorZ_A + (s.FloorZ_B - s.FloorZ_A) * t;
                            float exactCeilZ = s.CeilZ_A + (s.CeilZ_B - s.CeilZ_A) * t;

                            // Check against interpolated Z with vertical tolerance
                            if (position.Z >= exactFloorZ - _builtVTolerance &&
                                position.Z <= exactCeilZ + _builtVTolerance)
                            {
                                segmentIndex = segIdx;
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        public static bool IsInside(Vector3 position)
        {
            return TryGetContainingSegment(position, out _);
        }

        // ==========================================
        // INTERNAL HELPERS
        // ==========================================

        private static void AddToCells(float minX, float maxX, float minY, float maxY, int segmentIndex)
        {
            if (float.IsNaN(minX) || float.IsNaN(maxX) || float.IsNaN(minY) || float.IsNaN(maxY))
            {
                return;
            }

            int minCX = CellCoord(minX);
            int maxCX = CellCoord(maxX);
            int minCY = CellCoord(minY);
            int maxCY = CellCoord(maxY);

            if (maxCX < minCX || maxCY < minCY)
            {
                return;
            }

            for (int cx = minCX; cx <= maxCX; cx++)
            {
                for (int cy = minCY; cy <= maxCY; cy++)
                {
                    long key = GetKey(cx, cy);

                    if (!_cells.TryGetValue(key, out List<int> list))
                    {
                        list = new List<int>();
                        _cells[key] = list;
                    }

                    list.Add(segmentIndex);
                }
            }
        }

        private static int CellCoord(float value)
        {
            return (int)Math.Floor(value / _CELL_SIZE);
        }

        private static long GetKey(int x, int y)
        {
            return ((long)x << 32) | (uint)y;
        }

        private static void NextQueryStamp()
        {
            _queryStamp++;

            if (_queryStamp == int.MaxValue)
            {
                if (_lastSeen.Length > 0)
                {
                    Array.Clear(_lastSeen, 0, _lastSeen.Length);
                }

                _queryStamp = 1;
            }
        }

        private static Vector2 ExpandAwayFromCenter(Vector2 wall, Vector2 center, float tolerance)
        {
            float dx = wall.X - center.X;
            float dy = wall.Y - center.Y;

            float lenSq = dx * dx + dy * dy;

            if (lenSq < 1e-8f)
            {
                return wall;
            }

            float invLen = 1f / (float)Math.Sqrt(lenSq);

            return new Vector2(
                wall.X + dx * invLen * tolerance,
                wall.Y + dy * invLen * tolerance
            );
        }

        private static Vector3 EstimateWall(TunnelPoint p, int side)
        {
            float rad = p.HeadingDeg * (float)Math.PI / 180f;

            Vector3 rightVec = new Vector3(
                (float)Math.Cos(rad + Math.PI / 2f),
                (float)Math.Sin(rad + Math.PI / 2f),
                0f
            );

            return p.Center + rightVec * (p.Width * 0.5f * side);
        }

        private static float Min4(float a, float b, float c, float d)
        {
            return Math.Min(Math.Min(a, b), Math.Min(c, d));
        }

        private static float Max4(float a, float b, float c, float d)
        {
            return Math.Max(Math.Max(a, b), Math.Max(c, d));
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.X * b.Y - a.Y * b.X;
        }

        private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(b - a, p - a);
            float d2 = Cross(c - b, p - b);
            float d3 = Cross(a - c, p - c);

            bool hasNeg = d1 < 0f || d2 < 0f || d3 < 0f;
            bool hasPos = d1 > 0f || d2 > 0f || d3 > 0f;

            return !(hasNeg && hasPos);
        }

        private static bool PointInQuad(Vector2 p, Vector2 aLeft, Vector2 bLeft, Vector2 bRight, Vector2 aRight)
        {
            return PointInTriangle(p, aLeft, bLeft, bRight) ||
                   PointInTriangle(p, aLeft, bRight, aRight);
        }

        private static bool IsValidWall(Vector3 v)
        {
            return !float.IsNaN(v.X) && !float.IsNaN(v.Y) && !float.IsNaN(v.Z);
        }
    }
}