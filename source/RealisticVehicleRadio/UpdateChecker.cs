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
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

using static Helpers;

using GTA.UI;

public static class UpdateChecker
{
    public const string CurrentScriptVersion = "v1.0.0";
    private const string _MOD_PAGE_URL = "PASTE_REAL_LINK_HERE";
    private const string _VERSION_REGEX_PATTERN = @"Realistic Vehicle Radio\s+v(\d+\.\d+\.\d+)";

    public static async Task CheckForUpdatesAsync()
    {
        try
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "RealisticVehicleRadio-UpdateChecker/1.0"); // Be polite to the server
                client.Timeout = TimeSpan.FromSeconds(15); // Set a timeout

                string htmlContent = await client.GetStringAsync(_MOD_PAGE_URL);

                // Use Regex to find the version on the page
                Match match = Regex.Match(htmlContent, _VERSION_REGEX_PATTERN, RegexOptions.IgnoreCase);
                if (match.Success && match.Groups.Count > 1)
                {
                    string remoteVersion = "v" + match.Groups[1].Value; // Reconstruct full version string like "v3.0"

                    // Simple string comparison should work for standard versioning like v1.0, v1.1, v2.0, v3.1 etc.
                    int versionComparison = string.Compare(CurrentScriptVersion, remoteVersion, StringComparison.OrdinalIgnoreCase);

                    if (versionComparison < 0) // Current < Remote (e.g., "v3.0" < "v3.1")
                    {
                        Notification.PostTicker($"~g~[Realistic Vehicle Radio] Update Available! ~w~{remoteVersion}~n~~b~Download from gta5-mods.com", true);
                    }
                    else if (versionComparison > 0)
                    {

                    }
                    else // Current == Remote
                    {
                        Notification.PostTicker($"~g~[Realistic Vehicle Radio] Up to date. Version: {CurrentScriptVersion}", true);
                    }
                }
                else
                {
                    // Optionally notify user that check failed (maybe page format changed?)
                    Notification.PostTicker("~o~[Realistic Vehicle Radio] Update Check: Could not parse version.", true);
                }
            }
        }
        catch (HttpRequestException httpEx)
        {
            // Optionally notify user: Network issue
            Logger.DisplayAndLogError(httpEx, "~o~[Realistic Vehicle Radio] Update Check: Network error.");
        }
        catch (TaskCanceledException tcEx) // Likely a timeout
        {
            // Optionally notify user: Timeout
            Logger.DisplayAndLogError(tcEx, "~o~[Realistic Vehicle Radio] Update Check: Timed out.");
        }
        catch (Exception ex)
        {
            // Optionally notify user: Unexpected error
            Logger.DisplayAndLogError(ex, "~o~[Realistic Vehicle Radio] Update Check: An error occurred.");
        }
    }
}