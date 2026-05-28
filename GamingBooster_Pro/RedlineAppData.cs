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
        /// <summary>Kunden-Kauf/Key: erst true wenn Konto + Zahlung angebunden sind.</summary>
        public const bool ProPurchaseEnabled = false;

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

        public bool IsProActive => ProLicenseActive || DevProEnabled;

        public string ProSourceLabel
        {
            get
            {
                if (DevProEnabled)
                    return Language == "EN" ? "Developer" : "Entwickler";
                if (ProLicenseActive || Tier == LicenseTier.Pro)
                    return Language == "EN" ? "Lifetime license" : "Lifetime-Lizenz";
                return Language == "EN" ? "Free" : "Free";
            }
        }

        private static readonly string[] DevProKeys =
        {
            "REDLINE-PRO-V9-IMMISCH",
            "REDLINE-PRO-TOBIAS",
            "RLG-PRO-2026",
            "REDLINE-PRO-LIFETIME-DEV"
        };

        public static bool IsDevProKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return false;
            return DevProKeys.Any(k => string.Equals(k, key.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        public static bool LooksLikeProKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return false;

            key = key.Trim();
            if (IsDevProKey(key))
                return true;

            if (!ProPurchaseEnabled)
                return false;

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

            bool isDev = IsDevProKey(key);

            if (!ProPurchaseEnabled && !isDev)
            {
                error = Language == "EN"
                    ? "Pro purchase is not active yet. Payment and account linking will be enabled in a later update (10 EUR lifetime planned). You are on the free version."
                    : "Pro-Kauf ist noch nicht aktiv. Zahlung und Konto-Verknüpfung kommen in einem späteren Update (10 € Lifetime geplant). Du nutzt die Free-Version.";
                return false;
            }

            if (!LooksLikeProKey(key))
            {
                error = Language == "EN"
                    ? "Invalid license key."
                    : "Ungültiger Lizenzschlüssel.";
                return false;
            }

            if (isDev)
            {
                if (!RedlineDevAuth.IsAuthorizedDeveloperMachine())
                {
                    error = Language == "EN"
                        ? "Developer keys only work on the authorized developer PC (hardware binding)."
                        : "Entwickler-Keys funktionieren nur auf dem autorisierten Entwickler-PC (Hardware-Erkennung).";
                    return false;
                }
                DevProEnabled = true;
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
            DevProEnabled = false;
            if (Tier == LicenseTier.Pro)
                Tier = LicenseTier.Free;
            ProLicenseMasked = "";
        }

        public void EnforcePurchasePolicyOnLoad()
        {
            if (ProPurchaseEnabled)
                return;

            if (ProLicenseActive && !DevProEnabled)
            {
                ProLicenseActive = false;
                ProLicenseMasked = "";
                if (Tier == LicenseTier.Pro)
                    Tier = LicenseTier.Free;
            }
        }

        public void SanitizeDeveloperProState()
        {
            if (DevProEnabled && !RedlineDevAuth.IsAuthorizedDeveloperMachine())
            {
                DevProEnabled = false;
                if (!ProPurchaseEnabled || !ProLicenseActive)
                {
                    ProLicenseActive = false;
                    ProLicenseMasked = "";
                    if (Tier == LicenseTier.Pro)
                        Tier = LicenseTier.Free;
                }
            }
        }

        /// <summary>Entwickler-Pro automatisch auf autorisiertem PC (Tobias Hardware).</summary>
        public bool ApplyDeveloperProFromHardware()
        {
            if (!RedlineDevAuth.IsAuthorizedDeveloperMachine())
                return false;

            DevProEnabled = true;
            ProLicenseActive = true;
            Tier = LicenseTier.Pro;
            string pc = Environment.MachineName;
            ProLicenseMasked = pc.Length <= 4 ? "****" : "****" + pc[^4..];
            return true;
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

                EnforcePurchasePolicyOnLoad();
                SanitizeDeveloperProState();
                ApplyDeveloperProFromHardware();
            }
            catch { }
        }

        public void InitializeLicenseOnStartup()
        {
            try
            {
                if (!File.Exists(SettingsPath))
                    ApplyDeveloperProFromHardware();
                else
                    Load();
            }
            catch
            {
                ApplyDeveloperProFromHardware();
            }
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
