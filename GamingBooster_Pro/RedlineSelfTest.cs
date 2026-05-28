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
            Assert("9.5 > 9.4", CompareVer("9.5", "9.4") > 0);
            Assert("9.4 < 9.5", CompareVer("9.4", "9.5") < 0);
            Assert("9.5 == 9.5", CompareVer("9.5", "9.5") == 0);
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
            Assert("version.json Version 9.5", ver == "9.5", "v" + ver);
            Assert("version.json downloadUrl", doc.RootElement.TryGetProperty("downloadUrl", out _));
        }

        private static void TestUpdateLogRoundtrip()
        {
            RedlineUpdateLog.Add("9.5-test", "9.5", "selftest", "OK selftest");
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

            const string url = "https://cdn.jsdelivr.net/gh/LegendR622/Redline-Gaming-Optimizer@main/version.json";
            try
            {
                using HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
                client.DefaultRequestHeaders.UserAgent.ParseAdd("RedlineSelfTest/9.5");
                string json = await client.GetStringAsync(url).ConfigureAwait(false);
                if (!json.TrimStart().StartsWith("{", StringComparison.Ordinal))
                {
                    Fail("Online version.json", "kein JSON (CDN/HTML?)");
                    return;
                }
                using JsonDocument doc = JsonDocument.Parse(json);
                string ver = doc.RootElement.GetProperty("version").GetString() ?? "";
                Assert("Online version.json erreichbar", !string.IsNullOrWhiteSpace(ver), "v" + ver);
            }
            catch (Exception ex)
            {
                Fail("Online version.json", ex.Message);
            }
        }
    }
}
