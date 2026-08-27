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
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using static RealisticVehicleRadio;

using GTA.UI;

public static class TunnelSaveQueue
{
    private static readonly object _lock = new object();

    private static List<Tunnel> _pendingSnapshot;
    private static bool _saveRequested;
    private static bool _saveRunning;

    private static string _pendingMessage;

    // Set to true if you want background save notifications.
    public static bool NotifyUser { get; set; } = false;

    public static void QueueSave(List<Tunnel> tunnels)
    {
        if (tunnels == null)
        {
            return;
        }

        lock (_lock)
        {
            _pendingSnapshot = CreateSnapshot(tunnels);
            _saveRequested = true;
        }
    }

    public static void Process()
    {
        // 1. Show pending background-thread messages on the script thread.
        string message = null;

        lock (_lock)
        {
            if (_pendingMessage != null)
            {
                message = _pendingMessage;
                _pendingMessage = null;
            }
        }

        if (message != null)
        {
            Notification.PostTicker(message, true);
        }

        // 2. Start a new background save if one is pending and none is running.
        List<Tunnel> snapshot = null;
        bool startSave = false;

        lock (_lock)
        {
            if (_saveRequested && !_saveRunning)
            {
                startSave = true;
                _saveRunning = true;
                _saveRequested = false;

                snapshot = _pendingSnapshot;
                _pendingSnapshot = null;
            }
        }

        if (!startSave)
        {
            return;
        }

        Task.Run(() =>
        {
            try
            {
                // IMPORTANT:
                // This must be a thread-safe save method.
                // It must not call GTA natives, Notification, Screen, Function.Call, etc.
                FileOperations.SaveTunnels(snapshot);

                if (NotifyUser)
                {
                    lock (_lock)
                    {
                        _pendingMessage = "~g~Tunnel file saved.";
                    }
                }
            }
            catch (Exception ex)
            {
                lock (_lock)
                {
                    _pendingMessage = $"~r~Tunnel save failed: {ex.Message}";
                }
            }
            finally
            {
                lock (_lock)
                {
                    _saveRunning = false;
                }
            }
        });
    }

    private static List<Tunnel> CreateSnapshot(List<Tunnel> tunnels)
    {
        List<Tunnel> copy = new List<Tunnel>(tunnels.Count);

        for (int i = 0; i < tunnels.Count; i++)
        {
            Tunnel tunnel = tunnels[i];

            copy.Add(new Tunnel
            {
                Id = tunnel.Id,
                Name = tunnel.Name,
                Points = tunnel.Points == null
                    ? new List<TunnelPoint>()
                    : new List<TunnelPoint>(tunnel.Points)
            });
        }

        return copy;
    }

    public static void FlushBlocking(int timeoutMs = 3000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

        while (DateTime.UtcNow < deadline)
        {
            List<Tunnel> snapshot = null;

            lock (_lock)
            {
                if (!_saveRunning && !_saveRequested)
                {
                    return;
                }

                if (!_saveRunning && _saveRequested)
                {
                    snapshot = _pendingSnapshot;
                    _pendingSnapshot = null;
                    _saveRequested = false;
                }
            }

            if (snapshot != null)
            {
                try
                {
                    FileOperations.SaveTunnels(snapshot);
                }
                catch
                {
                    // Ignore during shutdown.
                }
            }
            else
            {
                Thread.Sleep(1);
            }
        }
    }
}