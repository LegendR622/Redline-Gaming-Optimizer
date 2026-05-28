using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text.RegularExpressions;

namespace GamingBooster_Pro
{
    internal sealed class DriverDisplayItem
    {
        public string DeviceName { get; set; } = "";
        public string Provider { get; set; } = "";
        public string Version { get; set; } = "";
        public string InfName { get; set; } = "";
        public string Signed { get; set; } = "";
        public DateTime? DriverDate { get; set; }
        public int? PnpErrorCode { get; set; }
        public string Status { get; set; } = "PRÜFEN";
        public string Detail { get; set; } = "";
        public string? WindowsUpdateTitle { get; set; }
        public bool FromProblemDevice { get; set; }
    }

    internal static class RedlineDriverStatus
    {
        private static readonly Dictionary<string, DateTime> RecentlyUpdatedUntil = new(StringComparer.OrdinalIgnoreCase);

        private static readonly string[] ImportantKeywords =
        {
            "nvidia", "geforce", "amd", "radeon", "advanced micro devices", "intel", "realtek",
            "mediatek", "qualcomm", "killer", "broadcom", "bluetooth", "wi-fi", "wifi",
            "ethernet", "audio", "chipset", "smbus", "gpio", "usb", "display", "monitor"
        };

        public static void MarkRecentlyUpdated(string deviceName)
        {
            if (string.IsNullOrWhiteSpace(deviceName))
                return;
            RecentlyUpdatedUntil[deviceName.Trim()] = DateTime.UtcNow.AddMinutes(15);
        }

        public static Dictionary<string, int> BuildDeviceErrorMap()
        {
            Dictionary<string, int> map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using ManagementObjectSearcher s = new ManagementObjectSearcher(
                    "SELECT Name, DeviceID, ConfigManagerErrorCode FROM Win32_PnPEntity WHERE ConfigManagerErrorCode <> 0");
                foreach (ManagementObject o in s.Get())
                {
                    int code = Convert.ToInt32(o["ConfigManagerErrorCode"] ?? 0);
                    string name = o["Name"]?.ToString() ?? "";
                    string id = o["DeviceID"]?.ToString() ?? "";
                    if (string.IsNullOrWhiteSpace(name))
                        continue;
                    map[name] = code;
                    if (!string.IsNullOrWhiteSpace(id))
                        map[id] = code;
                    foreach (string token in Tokenize(name))
                    {
                        if (token.Length >= 4 && !map.ContainsKey(token))
                            map[token] = code;
                    }
                }
            }
            catch { }

            return map;
        }

        public static List<DriverDisplayItem> BuildLeftPanelList(int max = 12)
        {
            Dictionary<string, int> errors = BuildDeviceErrorMap();
            List<DriverDisplayItem> fromWmi = LoadSignedDrivers();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<DriverDisplayItem> rows = new List<DriverDisplayItem>();

            foreach (DriverDisplayItem d in fromWmi)
            {
                if (!IsImportant(d))
                    continue;
                string key = d.DeviceName.Trim();
                if (!seen.Add(key))
                    continue;
                EnrichStatus(d, errors);
                rows.Add(d);
            }

            foreach (var kv in errors)
            {
                if (kv.Key.Length < 8 || kv.Key.Contains("\\") || kv.Key.Contains("&"))
                    continue;
                if (rows.Any(r => NamesLikelyMatch(r.DeviceName, kv.Key)))
                    continue;
                if (!ImportantKeywords.Any(k => kv.Key.Contains(k, StringComparison.OrdinalIgnoreCase)))
                    continue;

                DriverDisplayItem problem = new DriverDisplayItem
                {
                    DeviceName = kv.Key,
                    Provider = T("Gerät", "Device"),
                    Version = "-",
                    PnpErrorCode = kv.Value,
                    FromProblemDevice = true
                };
                EnrichStatus(problem, errors);
                rows.Add(problem);
            }

            return rows
                .OrderByDescending(ScorePriority)
                .ThenBy(r => r.DeviceName, StringComparer.OrdinalIgnoreCase)
                .Take(max)
                .ToList();
        }

        public static string ResolveStatusAfterRefresh(DriverDisplayItem item, Dictionary<string, int> errors)
        {
            if (RecentlyUpdatedUntil.TryGetValue(item.DeviceName, out DateTime until) && until > DateTime.UtcNow)
            {
                if (!errors.Keys.Any(k => NamesLikelyMatch(k, item.DeviceName)))
                    return "AKTUALISIERT";
            }

            return ComputeStatus(item, errors);
        }

        private static void EnrichStatus(DriverDisplayItem d, Dictionary<string, int> errors)
        {
            d.PnpErrorCode ??= FindErrorCode(d.DeviceName, errors);
            d.WindowsUpdateTitle = null;
            d.Status = ComputeStatus(d, errors);
            d.Detail = BuildDetail(d);
        }

        private static string ComputeStatus(DriverDisplayItem d, Dictionary<string, int> errors)
        {
            if (RecentlyUpdatedUntil.TryGetValue(d.DeviceName, out DateTime until) && until > DateTime.UtcNow)
            {
                if (!HasActiveError(d.DeviceName, errors))
                    return "AKTUALISIERT";
            }

            if (d.PnpErrorCode is 22 or 28 or 10 or 31 or 43)
                return "UPDATE EMPFOHLEN";

            if (HasActiveError(d.DeviceName, errors))
                return "UPDATE EMPFOHLEN";

            if (d.Provider.Contains("Microsoft", StringComparison.OrdinalIgnoreCase) && IsMicrosoftSystemDriver(d))
                return "SYSTEM";

            if (!d.DriverDate.HasValue)
                return "PRÜFEN";

            int days = (int)(DateTime.Now - d.DriverDate.Value).TotalDays;
            string all = (d.Provider + " " + d.DeviceName).ToLowerInvariant();
            bool gpu = all.Contains("nvidia") || all.Contains("radeon") || all.Contains("geforce");
            bool chipset = all.Contains("chipset") || all.Contains("smbus") || all.Contains("gpio");
            bool network = all.Contains("wi-fi") || all.Contains("wifi") || all.Contains("ethernet") || all.Contains("realtek");

            if (gpu)
            {
                if (days <= 365) return "AKTUELL";
                if (days <= 730) return "PRÜFEN";
                return "UPDATE EMPFOHLEN";
            }

            if (chipset || network || all.Contains("gpio") || all.Contains("bluetooth"))
            {
                if (days <= 730) return "AKTUELL";
                return "PRÜFEN";
            }

            if (days <= 730) return "AKTUELL";
            if (days <= 1460) return "PRÜFEN";
            return "PRÜFEN";
        }

        private static string BuildDetail(DriverDisplayItem d)
        {
            List<string> parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(d.Provider))
                parts.Add(d.Provider);
            if (!string.IsNullOrWhiteSpace(d.Version) && d.Version != "-")
                parts.Add("v" + (d.Version.Length > 18 ? d.Version[..18] + "…" : d.Version));
            if (d.DriverDate.HasValue)
                parts.Add(d.DriverDate.Value.ToShortDateString());
            if (d.PnpErrorCode is int code)
                parts.Add(ErrorLabel(code));
            return string.Join(" · ", parts);
        }

        private static int ScorePriority(DriverDisplayItem d)
        {
            return d.Status switch
            {
                "UPDATE EMPFOHLEN" => 100,
                "AKTUALISIERT" => 90,
                "PRÜFEN" => 50,
                "AKTUELL" => 10,
                "SYSTEM" => 1,
                _ => 0
            };
        }

        private static bool HasActiveError(string deviceName, Dictionary<string, int> errors)
        {
            return FindErrorCode(deviceName, errors) is int c && c != 0;
        }

        private static int? FindErrorCode(string deviceName, Dictionary<string, int> errors)
        {
            foreach (var kv in errors)
            {
                if (NamesLikelyMatch(deviceName, kv.Key))
                    return kv.Value;
            }
            return null;
        }

        public static bool NamesLikelyMatch(string a, string b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
                return false;
            if (a.Contains(b, StringComparison.OrdinalIgnoreCase) || b.Contains(a, StringComparison.OrdinalIgnoreCase))
                return true;

            HashSet<string> ta = Tokenize(a).ToHashSet(StringComparer.OrdinalIgnoreCase);
            int hits = Tokenize(b).Count(t => t.Length >= 4 && ta.Contains(t));
            return hits >= 2 || (hits >= 1 && (ta.Contains("nvidia") || ta.Contains("amd") || ta.Contains("intel") || ta.Contains("realtek")));
        }

        private static int MatchScore(string device, string title)
        {
            int score = 0;
            foreach (string t in Tokenize(device))
            {
                if (t.Length >= 4 && title.Contains(t, StringComparison.OrdinalIgnoreCase))
                    score++;
            }
            return score;
        }

        private static IEnumerable<string> Tokenize(string text)
        {
            foreach (Match m in Regex.Matches(text.ToLowerInvariant(), "[a-z0-9]{3,}"))
                yield return m.Value;
        }

        private static bool IsImportant(DriverDisplayItem d)
        {
            string all = (d.Provider + " " + d.DeviceName).ToLowerInvariant();
            return ImportantKeywords.Any(k => all.Contains(k));
        }

        private static bool IsMicrosoftSystemDriver(DriverDisplayItem d)
        {
            string all = (d.Provider + " " + d.DeviceName + " " + d.InfName).ToLowerInvariant();
            return all.Contains("processor") || all.Contains("wan miniport") || all.Contains("hid")
                || all.Contains("generic") || all.Contains("remote desktop") || all.Contains("basic render");
        }

        private static List<DriverDisplayItem> LoadSignedDrivers()
        {
            List<DriverDisplayItem> list = new List<DriverDisplayItem>();
            try
            {
                using ManagementObjectSearcher s = new ManagementObjectSearcher(
                    "SELECT DeviceName, DriverProviderName, DriverVersion, DriverDate, InfName, IsSigned FROM Win32_PnPSignedDriver");
                foreach (ManagementObject o in s.Get())
                {
                    list.Add(new DriverDisplayItem
                    {
                        DeviceName = o["DeviceName"]?.ToString() ?? "Unbekannt",
                        Provider = o["DriverProviderName"]?.ToString() ?? "Unbekannt",
                        Version = o["DriverVersion"]?.ToString() ?? "Unbekannt",
                        InfName = o["InfName"]?.ToString() ?? "",
                        Signed = o["IsSigned"]?.ToString() ?? "",
                        DriverDate = ParseWmiDate(o["DriverDate"]?.ToString())
                    });
                }
            }
            catch { }

            return list;
        }

        private static DateTime? ParseWmiDate(string? raw)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(raw) || raw.Length < 8)
                    return null;
                return new DateTime(int.Parse(raw[..4]), int.Parse(raw.Substring(4, 2)), int.Parse(raw.Substring(6, 2)));
            }
            catch { return null; }
        }

        private static string ErrorLabel(int code) => code switch
        {
            28 => T("Treiber fehlt", "Driver missing"),
            22 => T("Deaktiviert", "Disabled"),
            10 => T("Start fehlgeschlagen", "Start failed"),
            31 => T("Treiber ladefehler", "Driver load error"),
            _ => T("Fehler Code ", "Error code ") + code
        };

        private static string T(string de, string en) =>
            RedlineAppData.Current.Language == "EN" ? en : de;
    }
}
