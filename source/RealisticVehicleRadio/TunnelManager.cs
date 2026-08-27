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

using System.Linq;
using System.Collections.Generic;

using static RealisticVehicleRadio;

using GTA.UI;

public class Tunnel
{
    public int Id { get; set; }
    public string Name { get; set; }
    public List<TunnelPoint> Points { get; set; } = new List<TunnelPoint>();
}

public static class TunnelManager
{
    public static List<Tunnel> Tunnels { get; private set; } = new List<Tunnel>();
    private static int _nextId = 1;

    public static void Load()
    {
        Tunnels = FileOperations.LoadTunnels();

        if (Tunnels != null && Tunnels.Any())
        {
            _nextId = Tunnels.Max(t => t.Id) + 1;
        }

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

        TunnelSpatialIndex.MarkDirty();

        Save();

        Notification.PostTicker($"~g~Saved '{finalName}' (#{newId}, {points.Length} pts)", true);
    }

    public static void DeleteTunnel(int id)
    {
        Tunnel tunnel = Tunnels.FirstOrDefault(t => t.Id == id);
        string displayName = tunnel != null ? tunnel.Name : $"#{id}";

        Tunnels.RemoveAll(t => t.Id == id);

        TunnelSpatialIndex.MarkDirty();

        Save();

        Notification.PostTicker($"~r~Deleted Tunnel: {displayName}", true);
    }

    public static void Save()
    {
        TunnelSaveQueue.QueueSave(Tunnels);
    }
}