using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace GamingBooster_Pro
{
    internal sealed class RedlineUpdateManifest
    {
        public string Version { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
        public string Notes { get; set; } = "";
        public string Source { get; set; } = "";
    }

    internal static class RedlineOnlineUpdate
    {
        private static readonly string[] VersionJsonUrls =
        {
            "https://raw.githubusercontent.com/LegendR622/Redline-Gaming-Optimizer/main/version.json",
            "https://cdn.jsdelivr.net/gh/LegendR622/Redline-Gaming-Optimizer@main/version.json"
        };

        public static async Task<RedlineUpdateManifest?> FetchBestManifestAsync(HttpClient client, string installedVersion)
        {
            string cacheBust = "?t=" + DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            RedlineUpdateManifest? best = null;

            foreach (string baseUrl in VersionJsonUrls)
            {
                RedlineUpdateManifest? m = await TryFetchVersionJsonAsync(client, baseUrl + cacheBust, baseUrl);
                if (m == null)
                    continue;

                if (best == null || CompareVersions(m.Version, best.Version) > 0)
                    best = m;
            }

            RedlineUpdateManifest? release = await TryFetchGitHubLatestReleaseAsync(client);
            if (release != null)
            {
                if (best == null || CompareVersions(release.Version, best.Version) > 0)
                    best = release;
            }

            if (best == null)
                return null;

            if (CompareVersions(best.Version, installedVersion) < 0)
            {
                best.Notes = (string.IsNullOrWhiteSpace(best.Notes) ? "" : best.Notes + " | ")
                    + "Warnung: Online-Quelle wirkt veraltet (V" + best.Version + " < installiert V" + installedVersion + ").";
            }

            return best;
        }

        private static async Task<RedlineUpdateManifest?> TryFetchVersionJsonAsync(HttpClient client, string url, string sourceLabel)
        {
            try
            {
                string json = await client.GetStringAsync(url);
                json = json.Trim('\uFEFF', ' ', '\r', '\n');
                JsonNode? node = JsonNode.Parse(json);
                if (node == null)
                    return null;

                string version = node["version"]?.ToString()?.Trim() ?? "";
                string downloadUrl = node["downloadUrl"]?.ToString()?.Trim() ?? "";
                string notes = node["notes"]?.ToString()?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(downloadUrl))
                    return null;

                return new RedlineUpdateManifest
                {
                    Version = version,
                    DownloadUrl = downloadUrl,
                    Notes = notes,
                    Source = sourceLabel
                };
            }
            catch
            {
                return null;
            }
        }

        private static async Task<RedlineUpdateManifest?> TryFetchGitHubLatestReleaseAsync(HttpClient client)
        {
            try
            {
                const string api = "https://api.github.com/repos/LegendR622/Redline-Gaming-Optimizer/releases/latest";
                using HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, api);
                req.Headers.TryAddWithoutValidation("Accept", "application/vnd.github+json");
                req.Headers.TryAddWithoutValidation("User-Agent", "RedlineGamingOptimizer-Update");

                using HttpResponseMessage res = await client.SendAsync(req);
                if (!res.IsSuccessStatusCode)
                    return null;

                string json = await res.Content.ReadAsStringAsync();
                using JsonDocument doc = JsonDocument.Parse(json);
                JsonElement root = doc.RootElement;

                string tag = root.TryGetProperty("tag_name", out JsonElement tagEl)
                    ? tagEl.GetString() ?? ""
                    : "";
                string version = tag.TrimStart('v', 'V');
                string notes = root.TryGetProperty("body", out JsonElement bodyEl)
                    ? bodyEl.GetString() ?? ""
                    : "";

                string? zipUrl = null;
                if (root.TryGetProperty("assets", out JsonElement assets) && assets.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement asset in assets.EnumerateArray())
                    {
                        string name = asset.TryGetProperty("name", out JsonElement nameEl)
                            ? nameEl.GetString() ?? ""
                            : "";
                        if (!name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                            continue;

                        zipUrl = asset.TryGetProperty("browser_download_url", out JsonElement urlEl)
                            ? urlEl.GetString()
                            : null;
                        break;
                    }
                }

                if (string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(zipUrl))
                    return null;

                return new RedlineUpdateManifest
                {
                    Version = version,
                    DownloadUrl = zipUrl,
                    Notes = string.IsNullOrWhiteSpace(notes) ? "GitHub Release latest" : notes,
                    Source = "api.github.com/releases/latest"
                };
            }
            catch
            {
                return null;
            }
        }

        public static int CompareVersions(string online, string current)
        {
            try
            {
                return ParseVersionSafe(online).CompareTo(ParseVersionSafe(current));
            }
            catch
            {
                return string.Compare(online, current, StringComparison.OrdinalIgnoreCase);
            }
        }

        private static Version ParseVersionSafe(string value)
        {
            string cleaned = new string(value.Where(c => char.IsDigit(c) || c == '.').ToArray());
            if (string.IsNullOrWhiteSpace(cleaned))
                return new Version(0, 0);

            string[] parts = cleaned.Split('.', StringSplitOptions.RemoveEmptyEntries);
            while (parts.Length < 2)
                cleaned += ".0";

            return new Version(cleaned);
        }
    }
}
