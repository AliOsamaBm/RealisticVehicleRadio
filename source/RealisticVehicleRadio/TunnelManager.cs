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
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

using static RealisticVehicleRadio.Helpers.GUI;

namespace RealisticVehicleRadio
{
    public class Tunnel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public List<TunnelPoint> Points { get; set; } = new List<TunnelPoint>();
    }

    public static class TunnelManager
    {
        public static List<Tunnel> Tunnels { get; private set; } = new List<Tunnel>();
        // New in V1.3.0: O(1) lookup by tunnel Id
        private static readonly Dictionary<int, Tunnel> _tunnelsById = new Dictionary<int, Tunnel>();
        private static int _nextId = 1;
        public static event Action TunnelsChanged;

        // New in V1.3.0
        private static void RaiseTunnelsChanged()
        {
            TunnelsChanged?.Invoke();
        }

        /// <summary>
        /// Fast lookup. Returns null if the tunnel does not exist.
        /// </summary>
        public static Tunnel GetTunnelById(int id)
        {
            return _tunnelsById.TryGetValue(id, out Tunnel t) ? t : null;
        }

        private static void RebuildLookup()
        {
            _tunnelsById.Clear();
            for (int i = 0; i < Tunnels.Count; i++)
            {
                Tunnel t = Tunnels[i];
                if (t != null)
                {
                    _tunnelsById[t.Id] = t;
                }
            }
        }

        public static async Task LoadAsync()
        {
            List<Tunnel> loadedTunnels = await FileOperations.LoadTunnelsAsync();
            Tunnels = new List<Tunnel>();

            if (loadedTunnels != null)
            {
                HashSet<int> seenIds = new HashSet<int>();
                int maxId = 0;

                foreach (Tunnel tunnel in loadedTunnels)
                {
                    if (tunnel == null)
                    {
                        continue;
                    }

                    if (tunnel.Points == null)
                    {
                        tunnel.Points = new List<TunnelPoint>();
                    }

                    // A tunnel needs at least 2 points to form a segment
                    if (tunnel.Points.Count < 2)
                    {
                        continue;
                    }

                    if (tunnel.Name == null)
                    {
                        tunnel.Name = $"Tunnel {tunnel.Id}";
                    }

                    // Handle duplicate IDs by assigning a high temporary ID
                    if (!seenIds.Add(tunnel.Id))
                    {
                        tunnel.Id = maxId + 1000 + seenIds.Count;
                    }

                    Tunnels.Add(tunnel);
                    if (tunnel.Id > maxId)
                    {
                        maxId = tunnel.Id;
                    }
                }

                _nextId = maxId + 1;

                // Fix any temporary duplicate IDs to be sequential
                foreach (Tunnel tunnel in Tunnels)
                {
                    if (tunnel.Id >= maxId + 1000)
                    {
                        tunnel.Id = _nextId++;
                    }
                }
            }
            else
            {
                _nextId = 1;
            }

            RebuildLookup(); // sync
            TunnelSpatialIndex.MarkDirty();
        }

        public static void AddTunnel(TunnelPoint[] points, string customName = null)
        {
            int newId = _nextId++;

            string finalName = string.IsNullOrWhiteSpace(customName)
                ? $"Tunnel {newId}"
                : customName;

            Tunnel newTunnel = new Tunnel
            {
                Id = newId,
                Name = finalName,
                Points = points.ToList()
            };

            Tunnels.Add(newTunnel);

            _tunnelsById[newTunnel.Id] = newTunnel; // sync

            TunnelSpatialIndex.MarkDirty();

            Save();

            ShowContextualMessage($"~g~Saved '{finalName}' (#{newId}, {points.Length} pts)");

            RaiseTunnelsChanged();
        }

        public static void DeleteTunnel(int id)
        {
            Tunnel tunnel = GetTunnelById(id);
            if (tunnel == null)
            {
                ShowContextualMessage($"~r~Tunnel #{id} was not found.");
                return;
            }

            string displayName = tunnel != null ? tunnel.Name : $"#{id}";

            Tunnels.RemoveAll(t => t.Id == id);
            _tunnelsById.Remove(id); // sync

            TunnelSpatialIndex.MarkDirty();

            Save();

            ShowContextualMessage($"~r~Deleted Tunnel: {displayName}");

            RaiseTunnelsChanged();
        }

        /// <summary>
        /// Updates the name of a tunnel and saves the changes to disk.
        /// </summary>
        public static void RenameTunnel(int id, string newName)
        {
            Tunnel tunnel = GetTunnelById(id);
            if (tunnel == null)
            {
                ShowContextualMessage($"~r~Tunnel #{id} was not found.");
                return;
            }

            tunnel.Name = newName;

            // Call your existing save method to write the new name to the JSON file
            Save();
            RaiseTunnelsChanged();
        }

        public static void Save()
        {
            TunnelSaveQueue.QueueSave(Tunnels);
        }
    }
}