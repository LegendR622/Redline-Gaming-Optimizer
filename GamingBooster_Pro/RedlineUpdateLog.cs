using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace GamingBooster_Pro
{
    internal sealed class RedlineUpdateLog
    {
        public sealed class Entry
        {
            public DateTime Utc { get; set; }
            public string InstalledVersion { get; set; } = "";
            public string OnlineVersion { get; set; } = "";
            public string Notes { get; set; } = "";
            public string Result { get; set; } = "";
        }

        private static string LogPath => Path.Combine(RedlineAppData.AppFolder, "update-log.json");

        public static void Add(string installedVersion, string onlineVersion, string notes, string result)
        {
            try
            {
                List<Entry> list = LoadAll();
                list.Insert(0, new Entry
                {
                    Utc = DateTime.UtcNow,
                    InstalledVersion = installedVersion ?? "",
                    OnlineVersion = onlineVersion ?? "",
                    Notes = notes ?? "",
                    Result = result ?? ""
                });
                if (list.Count > 40)
                    list = list.Take(40).ToList();

                File.WriteAllText(LogPath, JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        public static List<Entry> LoadAll()
        {
            try
            {
                if (!File.Exists(LogPath))
                    return new List<Entry>();
                string json = File.ReadAllText(LogPath);
                return JsonSerializer.Deserialize<List<Entry>>(json) ?? new List<Entry>();
            }
            catch
            {
                return new List<Entry>();
            }
        }

        private static bool IsTestEntry(Entry e)
        {
            if (e.InstalledVersion.Contains("test", StringComparison.OrdinalIgnoreCase)
                || e.OnlineVersion.Contains("test", StringComparison.OrdinalIgnoreCase))
                return true;
            if (e.Result.Contains("selftest", StringComparison.OrdinalIgnoreCase))
                return true;
            if (e.Notes.Contains("selftest", StringComparison.OrdinalIgnoreCase))
                return true;
            return false;
        }

        public static Entry? GetLatestRealEntry()
        {
            return LoadAll().FirstOrDefault(e => !IsTestEntry(e));
        }

        /// <summary>Kurzer Status für die Update-Seite – eine Zeile reicht.</summary>
        public static string FormatSimpleStatus(bool english, string currentVersion)
        {
            Entry? latest = GetLatestRealEntry();
            if (latest == null)
            {
                return english
                    ? "Ready. Redline V" + currentVersion + " — click Check to verify online version."
                    : "Bereit. Redline V" + currentVersion + " — mit „Nur prüfen“ online Version prüfen.";
            }

            string result = latest.Result ?? "";
            bool upToDate = result.Contains("Neueste Version", StringComparison.OrdinalIgnoreCase)
                || result.Contains("Latest version", StringComparison.OrdinalIgnoreCase)
                || result.Contains("Aktuell (installiert", StringComparison.OrdinalIgnoreCase)
                || result.Contains("Up to date", StringComparison.OrdinalIgnoreCase);

            if (upToDate)
            {
                return english
                    ? "✅ Latest version is current (V" + currentVersion + ")."
                    : "✅ Neueste Version ist aktuell (V" + currentVersion + ").";
            }

            if (result.Contains("Update verfügbar", StringComparison.OrdinalIgnoreCase)
                || result.Contains("Update available", StringComparison.OrdinalIgnoreCase))
            {
                return english
                    ? "⬆ Update available: V" + latest.OnlineVersion + " (installed V" + currentVersion + ")."
                    : "⬆ Update verfügbar: V" + latest.OnlineVersion + " (installiert V" + currentVersion + ").";
            }

            return result;
        }
    }
}
