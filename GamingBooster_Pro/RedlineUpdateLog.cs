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

        public static string FormatForUi(bool english)
        {
            List<Entry> list = LoadAll();
            if (list.Count == 0)
                return english
                    ? "No update checks yet. History appears here after each check."
                    : "Noch keine Update-Prüfungen. Verlauf erscheint nach jedem Check.";

            List<string> lines = new List<string>
            {
                english ? "----- UPDATE HISTORY -----" : "----- UPDATE-VERLAUF -----"
            };

            foreach (Entry e in list.Take(12))
            {
                string when = e.Utc.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
                lines.Add(when + "  |  " + e.Result);
                lines.Add("  Installiert: " + e.InstalledVersion + "  →  Online: " + e.OnlineVersion);
                if (!string.IsNullOrWhiteSpace(e.Notes))
                    lines.Add("  " + e.Notes);
                lines.Add("");
            }

            return string.Join(Environment.NewLine, lines).TrimEnd();
        }
    }
}
