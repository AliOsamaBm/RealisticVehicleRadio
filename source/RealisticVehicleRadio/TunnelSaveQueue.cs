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
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using static RealisticVehicleRadio.Helpers.GUI;

namespace RealisticVehicleRadio
{
    public static class TunnelSaveQueue
    {
        private static readonly object _lock = new object();

        private static List<Tunnel> _pendingSnapshot;
        private static bool _saveRequested;
        private static bool _saveRunning;

        private static string _pendingMessage;

        // Set to true if you want background save GUI.ShowContextualMessages.
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
            ShowPendingMessage();
            StartPendingSaveIfReady();
        }

        private static void ShowPendingMessage()
        {
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
                ShowContextualMessage(message);
            }
        }

        private static void StartPendingSaveIfReady()
        {
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

            Task.Run(async () =>
            {
                try
                {
                    await FileOperations.SaveTunnelsAsync(snapshot);
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
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

            while (DateTime.UtcNow < deadline)
            {
                List<Tunnel> snapshot = TryDequeuePendingSave();

                if (snapshot == null && !IsSaveActive())
                {
                    return; // Nothing pending, nothing running
                }

                if (snapshot != null)
                {
                    try
                    {
                        FileOperations.SaveTunnelsAsync(snapshot).GetAwaiter().GetResult();
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

        private static List<Tunnel> TryDequeuePendingSave()
        {
            lock (_lock)
            {
                if (!_saveRunning && _saveRequested)
                {
                    var snapshot = _pendingSnapshot;
                    _pendingSnapshot = null;
                    _saveRequested = false;
                    return snapshot;
                }
                return null;
            }
        }

        private static bool IsSaveActive()
        {
            lock (_lock)
            {
                return _saveRunning || _saveRequested;
            }
        }
    }
}