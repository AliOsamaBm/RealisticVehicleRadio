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

using GTA;

namespace RealisticVehicleRadio
{
    public sealed class VehicleExclusionManager
    {
        private const int _HASH_SET_THRESHOLD = 4;

        // Hybrid exclusion lookup: Array for small lists, HashSet for large lists
        private VehicleHash[] _lastSeenArray;
        private HashSet<VehicleHash> _hashSet;
        private bool _useHashSet;

        public bool IsVehicleExcluded(VehicleHash[] configArray, VehicleHash hash)
        {
            if (configArray != _lastSeenArray)
            {
                _lastSeenArray = configArray ?? Array.Empty<VehicleHash>();

                // Switch to HashSet if the list is large, otherwise stick to fast Array search
                if (_lastSeenArray.Length >= _HASH_SET_THRESHOLD)
                {
                    _hashSet = new HashSet<VehicleHash>(_lastSeenArray);
                    _useHashSet = true;
                }
                else
                {
                    _hashSet = null;
                    _useHashSet = false;
                }
            }

            // O(1) Lookup for large lists
            if (_useHashSet)
            {
                return _hashSet.Contains(hash);
            }

            // O(N) Linear search for small lists (Extremely fast due to CPU cache locality)
            for (int i = 0; i < _lastSeenArray.Length; i++)
            {
                if (_lastSeenArray[i] == hash)
                {
                    return true;
                }
            }

            return false;
        }
    }
}