using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace GamingBooster_Pro
{
    public enum LicenseTier
    {
        Free,
        Pro
    }

    /// <summary>
    /// Persistente App-Einstellungen und Scan-Ergebnisse (lokal, AppData).
    /// </summary>
    public sealed class RedlineAppData
    {
        public static RedlineAppData Current { get; } = new();

        public LicenseTier Tier { get; set; } = LicenseTier.Free;
        public bool DevProEnabled { get; set; }
        public bool ProLicenseActive { get; set; }
        public string ProLicenseMasked { get; set; } = "";
        public string ScanDepth { get; set; } = "Standard";
        public string Language { get; set; } = "DE";
        public bool Notifications { get; set; } = true;
        public string Theme { get; set; } = "Dark";
        public bool AiAssistantEnabled { get; set; } = true;
        public bool FastIntro { get; set; } = true;
        public string GraphicsMode { get; set; } = "FPS";

        public DateTime? LastScanUtc { get; set; }
        public int? GamingScore { get; set; }
        public int? LastPingMs { get; set; }
        public bool SecurityChecked { get; set; }
        public int? SecurityScore { get; set; }

        /// <summary>Pro aktiv wenn Lifetime-Key oder Entwickler-Test.</summary>
        public bool IsProActive => ProLicenseActive || DevProEnabled;

        public string ProSourceLabel
        {
            get
            {
                if (ProLicenseActive || Tier == LicenseTier.Pro)
                    return Language == "EN" ? "Lifetime license" : "Lifetime-Lizenz";
                if (DevProEnabled)
                    return Language == "EN" ? "Developer" : "Entwickler";
                return Language == "EN" ? "Free" : "Free";
            }
        }

        /// <summary>Entwickler- und Lifetime-Schlüssel (offline). Kunden-Keys: REDLINE-PRO-LIFETIME-XXXX</summary>
        private static readonly string[] ValidProKeys =
        {
            "REDLINE-PRO-V9-IMMISCH",
            "REDLINE-PRO-TOBIAS",
            "RLG-PRO-2026",
            "REDLINE-PRO-LIFETIME-DEV"
        };

        public static bool LooksLikeProKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return false;

            key = key.Trim();
            if (ValidProKeys.Any(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase)))
                return true;

            return key.StartsWith("REDLINE-PRO-LIFETIME-", StringComparison.OrdinalIgnoreCase)
                && key.Length >= 24;
        }

        public bool TryActivateLicenseKey(string input, out string error)
        {
            string key = (input ?? "").Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                error = Language == "EN" ? "Enter a license key." : "Bitte einen Lizenzschlüssel eingeben.";
                return false;
            }

            if (!LooksLikeProKey(key))
            {
                error = Language == "EN"
                    ? "Invalid license key. Pro costs 10 EUR once (lifetime) – key from purchase email."
                    : "Ungültiger Lizenzschlüssel. Pro kostet einmalig 10 € (Lifetime) – Key aus der Kauf-Mail.";
                return false;
            }

            ProLicenseActive = true;
            Tier = LicenseTier.Pro;
            ProLicenseMasked = key.Length <= 4 ? "****" : "****" + key[^4..];
            error = "";
            return true;
        }

        public void DeactivateLicenseKey()
        {
            ProLicenseActive = false;
            if (Tier == LicenseTier.Pro && !DevProEnabled)
                Tier = LicenseTier.Free;
            ProLicenseMasked = "";
        }

        public string LastScanLabel
        {
            get
            {
                if (!LastScanUtc.HasValue)
                    return Language == "EN" ? "No scan yet" : "Noch kein Scan";
                return (Language == "EN" ? "Today, " : "Heute, ") + LastScanUtc.Value.ToLocalTime().ToString("HH:mm");
            }
        }

        public static string AppFolder
        {
            get
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RedlineGamingOptimizer");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        private static string SettingsPath => Path.Combine(AppFolder, "settings.json");

        public void Load()
        {
            try
            {
                if (!File.Exists(SettingsPath))
                    return;

                using FileStream fs = File.OpenRead(SettingsPath);
                RedlineSettingsDto? dto = JsonSerializer.Deserialize<RedlineSettingsDto>(fs);
                if (dto == null)
                    return;

                if (Enum.TryParse(dto.Tier, true, out LicenseTier tier))
                    Tier = tier;
                DevProEnabled = dto.DevProEnabled;
                ProLicenseActive = dto.ProLicenseActive;
                ProLicenseMasked = dto.ProLicenseMasked ?? "";
                if (ProLicenseActive)
                    Tier = LicenseTier.Pro;
                ScanDepth = string.IsNullOrWhiteSpace(dto.ScanDepth) ? "Standard" : dto.ScanDepth;
                Language = string.IsNullOrWhiteSpace(dto.Language) ? "DE" : dto.Language;
                Notifications = dto.Notifications;
                Theme = string.IsNullOrWhiteSpace(dto.Theme) ? "Dark" : dto.Theme;
                AiAssistantEnabled = dto.AiAssistantEnabled;
                FastIntro = dto.FastIntro;
                GraphicsMode = string.IsNullOrWhiteSpace(dto.GraphicsMode) ? "FPS" : dto.GraphicsMode;
                LastScanUtc = dto.LastScanUtc;
                GamingScore = dto.GamingScore;
                LastPingMs = dto.LastPingMs;
                SecurityChecked = dto.SecurityChecked;
                SecurityScore = dto.SecurityScore;
            }
            catch { }
        }

        public void Save()
        {
            try
            {
                RedlineSettingsDto dto = new RedlineSettingsDto
                {
                    Tier = Tier.ToString(),
                    DevProEnabled = DevProEnabled,
                    ProLicenseActive = ProLicenseActive,
                    ProLicenseMasked = ProLicenseMasked,
                    ScanDepth = ScanDepth,
                    Language = Language,
                    Notifications = Notifications,
                    Theme = Theme,
                    AiAssistantEnabled = AiAssistantEnabled,
                    FastIntro = FastIntro,
                    GraphicsMode = GraphicsMode,
                    LastScanUtc = LastScanUtc,
                    GamingScore = GamingScore,
                    LastPingMs = LastPingMs,
                    SecurityChecked = SecurityChecked,
                    SecurityScore = SecurityScore
                };

                string json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
            }
            catch { }
        }

        private sealed class RedlineSettingsDto
        {
            public string Tier { get; set; } = "Free";
            public bool DevProEnabled { get; set; }
            public bool ProLicenseActive { get; set; }
            public string ProLicenseMasked { get; set; } = "";
            public string ScanDepth { get; set; } = "Standard";
            public string Language { get; set; } = "DE";
            public bool Notifications { get; set; } = true;
            public string Theme { get; set; } = "Dark";
            public bool AiAssistantEnabled { get; set; } = true;
            public bool FastIntro { get; set; } = true;
            public string GraphicsMode { get; set; } = "FPS";
            public DateTime? LastScanUtc { get; set; }
            public int? GamingScore { get; set; }
            public int? LastPingMs { get; set; }
            public bool SecurityChecked { get; set; }
            public int? SecurityScore { get; set; }
        }
    }
}
