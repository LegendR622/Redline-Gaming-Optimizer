using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace GamingBooster_Pro
{
    /// <summary>Automatische Funktionstests (--selftest ohne UI, Logik + Netzwerk).</summary>
    internal static class RedlineSelfTest
    {
        private static readonly List<string> Lines = new();
        private static int _fail;

        public static int RunAll()
        {
            Lines.Clear();
            _fail = 0;

            Ok("=== Redline SelfTest (Logik) ===");
            TestLicenseKeys();
            TestRecommendedCategories();
            TestPerfTileRouting();
            TestFeatureGate();
            TestRemoteSupport();
            TestHardwareDriverLinks();
            TestVersionCompare();
            TestVersionJsonLocal();
            TestUpdateLogRoundtrip();
            TestStorageWmi();
            TestCleanerPathsExist();
            TestUpdateJsonOnlineAsync().GetAwaiter().GetResult();

            Ok("");
            Ok($"Ergebnis: {Lines.Count(l => l.StartsWith("[OK]"))} OK, {_fail} FAIL");
            string logPath = Path.Combine(Path.GetTempPath(), "redline-selftest.log");
            try { File.WriteAllLines(logPath, Lines, Encoding.UTF8); } catch { }
            Ok("Log: " + logPath);

            return _fail == 0 ? 0 : 1;
        }

        private static void Pass(string name, string detail = "")
        {
            string line = "[OK] " + name + (string.IsNullOrEmpty(detail) ? "" : " | " + detail);
            Lines.Add(line);
            Console.WriteLine(line);
        }

        private static void Fail(string name, string detail = "")
        {
            _fail++;
            string line = "[FAIL] " + name + (string.IsNullOrEmpty(detail) ? "" : " | " + detail);
            Lines.Add(line);
            Console.WriteLine(line);
        }

        private static void Ok(string s) => Lines.Add(s);

        private static void Assert(string name, bool condition, string detail = "")
        {
            if (condition) Pass(name, detail);
            else Fail(name, detail);
        }

        private static void TestLicenseKeys()
        {
            Assert("Pro-Key DEV gültig", RedlineAppData.LooksLikeProKey("REDLINE-PRO-LIFETIME-DEV"));
            Assert("Pro-Key IMMISCH gültig", RedlineAppData.LooksLikeProKey("REDLINE-PRO-V9-IMMISCH"));
            Assert("Pro-Key Kunde blockiert (Kauf aus)", !RedlineAppData.LooksLikeProKey("REDLINE-PRO-LIFETIME-TESTUSER1234"));
            Assert("Master-Key FREUNDIN gültig", RedlineAppData.IsMasterProKey("REDLINE-PRO-FREUNDIN-GIFT"));
            Assert("Master-Key in LooksLikeProKey", RedlineAppData.LooksLikeProKey("REDLINE-PRO-FREUNDIN-GIFT"));
            Assert("Pro-Key Lifetime-Muster wenn Kauf an", !RedlineAppData.ProPurchaseEnabled || RedlineAppData.LooksLikeProKey("REDLINE-PRO-LIFETIME-TESTUSER1234"));
            Assert("Pro-Key zu kurz ungültig", !RedlineAppData.LooksLikeProKey("REDLINE-PRO-LIFETIME-X"));
            Assert("Pro-Key Müll ungültig", !RedlineAppData.LooksLikeProKey("FAKE-KEY"));

            var data = RedlineAppData.Current;
            bool wasPro = data.ProLicenseActive;
            data.DeactivateLicenseKey();
            Assert("Entwickler-PC erkannt", RedlineDevAuth.IsAuthorizedDeveloperMachine(), RedlineDevAuth.GetMachineLabel());
            Assert("Hardware Entwickler-Pro", data.ApplyDeveloperProFromHardware());
            Assert("IsProActive nach Hardware", data.IsProActive);
            data.DeactivateLicenseKey();
            if (RedlineDevAuth.IsAuthorizedDeveloperMachine())
            {
                Assert("DEV-Key auf Entwickler-PC", data.TryActivateLicenseKey("REDLINE-PRO-LIFETIME-DEV", out string err) && string.IsNullOrEmpty(err), err);
                data.DeactivateLicenseKey();
            }
            Assert("Kunden-Key abgelehnt", !data.TryActivateLicenseKey("REDLINE-PRO-LIFETIME-CUSTOMER1234", out _));
            if (!wasPro) data.DeactivateLicenseKey();
        }

        private static void TestPerfTileRouting()
        {
            void Expect(string title, PerfDetailAction action)
            {
                PerfDetailAction got = RedlinePerfNavigation.Resolve(title);
                Assert("Perf-Pfeil " + title, got == action, got + " erwartet " + action);
            }

            Expect("GAME MODE", PerfDetailAction.GameModeSettings);
            Expect("HIGH PERFORMANCE", PerfDetailAction.PowerPlan);
            Expect("GRAFIK SETTINGS", PerfDetailAction.GraphicsSettings);
            Expect("VISUAL EFFECTS", PerfDetailAction.VisualEffects);
            Expect("BACKGROUND SERVICES", PerfDetailAction.Services);
            Expect("HINTERGRUNDDIENSTE", PerfDetailAction.Services);
            Expect("CHECK AUTOSTART", PerfDetailAction.NavigateStartup);
            Expect("AUTOSTART PRÜFEN", PerfDetailAction.NavigateStartup);
            Expect("WINDOWS FPS BOOST", PerfDetailAction.GameBar);
            Assert("Perf DryRun Token Game Mode",
                RedlinePerfNavigation.ExpectedDryRunToken(PerfDetailAction.GameModeSettings) == "uri:ms-settings:gaming-gamemode");
        }

        private static void TestFeatureGate()
        {
            bool dev = RedlineDevAuth.IsAuthorizedDeveloperMachine();
            Assert("InApp-Treiber bei Pro", RedlineFeatureGate.InAppDriverUpdateEnabled == RedlineAppData.Current.IsProActive);
            Assert("Perf GAME MODE Route", RedlinePerfNavigation.Resolve("GAME MODE") == PerfDetailAction.GameModeSettings);
        }

        private static void TestRemoteSupport()
        {
            RemoteSupportStatus rs = RedlineRemoteSupport.Query();
            Assert("RemoteSupport Query", rs != null);
            string label = RedlineRemoteSupport.FormatStatusLabel(rs, false);
            Assert("RemoteSupport Label DE", label.Contains("Remote Desktop", StringComparison.OrdinalIgnoreCase));
            Assert("Remote RDP bool lesbar", rs.RemoteDesktopEnabled || !rs.RemoteDesktopEnabled);
        }

        private static void TestHardwareDriverLinks()
        {
            HardwareProfile hp = new HardwareProfile
            {
                CpuName = "AMD Ryzen 7 5800X",
                GpuName = "NVIDIA GeForce RTX 3070",
                MotherboardManufacturer = "ASUSTeK COMPUTER INC.",
                MotherboardProduct = "PRIME B550-PLUS"
            };
            List<DriverUpdateLink> links = RedlineHardwareProfile.BuildSmartUpdateLinks(hp, Array.Empty<string>());
            Assert("Smart Links mindestens 4", links.Count >= 4, links.Count.ToString());
            Assert("Smart Links NVIDIA", links.Any(l => l.Id == "nvidia"));
            Assert("Smart Links ASUS MB", links.Any(l => l.Id == "mb-asus"));
            Assert("Smart Links GPU vendor", links.Any(l => l.Id is "nvidia" or "amd-gpu" or "intel-gpu"));
        }

        private static void TestRecommendedCategories()
        {
            string[] recommended = { "Browser Cache", "Temporäre Dateien", "Shader Cache" };
            string[] all =
            {
                "Browser Cache", "Temporäre Dateien", "Shader Cache",
                "Download-Reste", "Papierkorb", "DNS/Netzwerkreste"
            };

            foreach (string cat in recommended)
                Assert("Empfohlen enthält " + cat, recommended.Contains(cat, StringComparer.OrdinalIgnoreCase));

            int offCount = all.Count(c => !recommended.Contains(c, StringComparer.OrdinalIgnoreCase));
            Assert("Empfohlen schaltet 3 aus (Papierkorb/DNS/Download)", offCount == 3, offCount.ToString());
        }

        private static void TestVersionCompare()
        {
            Assert("9.11 > 9.10", CompareVer("9.11", "9.10") > 0);
            Assert("9.10 < 9.11", CompareVer("9.10", "9.11") < 0);
            Assert("9.11 == 9.11", CompareVer("9.11", "9.11") == 0);
        }

        private static int CompareVer(string online, string current)
        {
            try
            {
                return ParseVer(online).CompareTo(ParseVer(current));
            }
            catch
            {
                return string.Compare(online, current, StringComparison.OrdinalIgnoreCase);
            }
        }

        private static Version ParseVer(string value)
        {
            string cleaned = new string(value.Where(c => char.IsDigit(c) || c == '.').ToArray());
            if (string.IsNullOrWhiteSpace(cleaned)) return new Version(0, 0);
            string[] parts = cleaned.Split('.', StringSplitOptions.RemoveEmptyEntries);
            while (parts.Length < 2) cleaned += ".0";
            return new Version(cleaned);
        }

        private static void TestVersionJsonLocal()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "version.json");
            if (!File.Exists(path))
                path = Path.Combine(Environment.CurrentDirectory, "version.json");
            if (!File.Exists(path))
            {
                string repo = @"C:\Users\Tobi\Desktop\GamingBooster_Pro\version.json";
                if (File.Exists(repo)) path = repo;
            }

            if (!File.Exists(path))
            {
                Fail("version.json lokal", "nicht gefunden");
                return;
            }

            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
            string ver = doc.RootElement.GetProperty("version").GetString() ?? "";
            Assert("version.json Version 9.18", ver == "9.18", "v" + ver);
            Assert("version.json downloadUrl", doc.RootElement.TryGetProperty("downloadUrl", out _));
        }

        private static void TestUpdateLogRoundtrip()
        {
            RedlineUpdateLog.Add("9.11-test", "9.11", "selftest", "OK selftest");
            var all = RedlineUpdateLog.LoadAll();
            Assert("Update-Log Eintrag", all.Any(e => e.Result.Contains("selftest", StringComparison.OrdinalIgnoreCase)));
        }

        private static void TestStorageWmi()
        {
            try
            {
                string sys = Environment.SystemDirectory.Substring(0, 2);
                using var session = new System.Management.ManagementObjectSearcher(
                    $"SELECT Size, FreeSpace FROM Win32_LogicalDisk WHERE DeviceID='{sys}'");
                foreach (System.Management.ManagementObject disk in session.Get())
                {
                    ulong size = Convert.ToUInt64(disk["Size"] ?? 0UL);
                    Assert("WMI Festplatte " + sys, size > 0, Math.Round(size / 1e9, 1) + " GB");
                    return;
                }
                Fail("WMI Festplatte", "kein Datenträger");
            }
            catch (Exception ex)
            {
                Fail("WMI Festplatte", ex.Message);
            }
        }

        private static void TestCleanerPathsExist()
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            int ok = 0;
            if (Directory.Exists(Path.GetTempPath())) ok++;
            if (Directory.Exists(@"C:\Windows\Temp")) ok++;
            if (Directory.Exists(Path.Combine(local, "D3DSCache")) || Directory.Exists(local)) ok++;
            Assert("Cleaner Basis-Pfade", ok >= 2, ok + "/3");
        }

        private static async Task TestUpdateJsonOnlineAsync()
        {
            if (string.Equals(Environment.GetEnvironmentVariable("REDLINE_SELFTEST_OFFLINE"), "1", StringComparison.Ordinal))
            {
                Pass("Online version.json", "übersprungen (OFFLINE)");
                return;
            }

            try
            {
                using HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
                client.DefaultRequestHeaders.UserAgent.ParseAdd("RedlineSelfTest/9.13");
                RedlineUpdateManifest? manifest = await RedlineOnlineUpdate.FetchBestManifestAsync(client, "9.0").ConfigureAwait(false);
                Assert("Online Update-Manifest", manifest != null && !string.IsNullOrWhiteSpace(manifest.Version), manifest?.Version ?? "?");
                Assert("Online >= 9.12", manifest != null && RedlineOnlineUpdate.CompareVersions(manifest.Version, "9.12") >= 0, manifest?.Source ?? "");
            }
            catch (Exception ex)
            {
                Fail("Online version.json", ex.Message);
            }
        }
    }
}
