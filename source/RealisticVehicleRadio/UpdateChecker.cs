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
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

using Newtonsoft.Json.Linq;

using static Helpers;

using GTA.UI;

public static class UpdateChecker
{
    public const string CurrentScriptVersion = "v1.2.0";

    private const string _GITHUB_OWNER = "AliOsamaBm";
    private const string _GITHUB_REPO = "RealisticVehicleRadio";

    private const string _GITHUB_API_URL =
        "https://api.github.com/repos/" + _GITHUB_OWNER + "/" + _GITHUB_REPO + "/releases/latest";

    // =====================================================================
    // FIX: Force TLS 1.2 for .NET Framework 4.8
    // =====================================================================
    static UpdateChecker()
    {
        /*
           GitHub API requires TLS 1.2. Older .NET Framework versions default to TLS 1.0/1.1 
           which causes the "Could not create SSL/TLS secure channel" error.
        */
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
    }

    // =====================================================================
    // A single shared HttpClient (avoids socket exhaustion)
    // =====================================================================
    private static readonly HttpClient _client = CreateClient();

    private static HttpClient CreateClient()
    {
        HttpClient client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        // GitHub API REQUIRES a User-Agent header, otherwise it returns 403.
        client.DefaultRequestHeaders.Add("User-Agent", "RealisticVehicleRadio-UpdateChecker/1.0");
        client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");

        return client;
    }

    public static async Task CheckForUpdatesAsync()
    {
        try
        {
            using (HttpResponseMessage response = await _client.GetAsync(_GITHUB_API_URL))
            {
                // 404 = the repository exists, but no Release has been published yet.
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    Notification.PostTicker("~o~[Realistic Vehicle Radio] Update Check: No releases published on GitHub yet.", true);
                    return;
                }

                response.EnsureSuccessStatusCode();

                string json = await response.Content.ReadAsStringAsync();
                JObject release = JObject.Parse(json);

                string remoteTag = release["tag_name"]?.ToString();

                if (string.IsNullOrWhiteSpace(remoteTag))
                {
                    Notification.PostTicker("~o~[Realistic Vehicle Radio] Update Check: Could not parse version.", true);
                    return;
                }

                if (IsNewer(remoteTag, CurrentScriptVersion))
                {
                    Notification.PostTicker(
                        $"~g~[Realistic Vehicle Radio] Update Available! ~w~{remoteTag}~n~~b~Download from the GitHub Releases page",
                        true);
                }
                else
                {
                    Notification.PostTicker($"~g~[Realistic Vehicle Radio] Up to date. Version: {CurrentScriptVersion}", true);
                }
            }
        }
        catch (HttpRequestException httpEx)
        {
            Logger.DisplayAndLogError(httpEx, "~o~[Realistic Vehicle Radio] Update Check: Network error.");
        }
        catch (TaskCanceledException tcEx) // Timeout
        {
            Logger.DisplayAndLogError(tcEx, "~o~[Realistic Vehicle Radio] Update Check: Timed out.");
        }
        catch (Exception ex)
        {
            Logger.DisplayAndLogError(ex, "~o~[Realistic Vehicle Radio] Update Check: An error occurred.");
        }
    }

    /// <summary>
    /// Proper semantic version comparison.
    /// Unlike string.Compare, this correctly handles v1.10.0 > v1.9.0.
    /// </summary>
    public static bool IsNewer(string remoteTag, string currentTag)
    {
        return ParseVersion(remoteTag) > ParseVersion(currentTag);
    }

    private static Version ParseVersion(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return new Version(0, 0, 0);
        }

        string cleaned = tag.Trim().TrimStart('v', 'V');

        if (Version.TryParse(cleaned, out Version result))
        {
            // Normalize missing parts (e.g. "1.0" -> 1.0.0)
            return new Version(
                Math.Max(result.Major, 0),
                Math.Max(result.Minor, 0),
                Math.Max(result.Build, 0));
        }

        // Fallback for tags like "1.0-beta"
        Match match = Regex.Match(cleaned, @"^(\d+)(?:\.(\d+))?(?:\.(\d+))?");

        if (match.Success)
        {
            int major = int.Parse(match.Groups[1].Value);
            int minor = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;
            int patch = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;
            return new Version(major, minor, patch);
        }

        return new Version(0, 0, 0);
    }
}