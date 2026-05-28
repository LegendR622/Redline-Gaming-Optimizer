using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GamingBooster_Pro
{
    internal sealed class RedlineDriverUpdateService
    {
        public static RedlineDriverUpdateService Instance { get; } = new();

        private CancellationTokenSource? _cts;

        public bool IsRunning { get; private set; }

        public void Cancel() => _cts?.Cancel();

        public async Task RunAsync(
            bool installAfterSearch,
            bool installOnly,
            Func<string, Task> log,
            bool isEnglish,
            CancellationToken externalCt = default)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(externalCt);
            CancellationToken ct = _cts.Token;
            IsRunning = true;
            try
            {
                if (!installOnly)
                {
                    await log(isEnglish
                        ? "Step 1: Searching Windows driver updates (in-app)…"
                        : "Schritt 1: Suche Windows-Treiber-Updates (in der App)…");

                    int count = await SearchWindowsDriverUpdatesAsync(log, isEnglish, ct);
                    if (count == 0)
                        await log(isEnglish
                            ? "No pending Windows driver updates found via Windows Update."
                            : "Keine ausstehenden Windows-Treiber-Updates über Windows Update gefunden.");
                    else
                        await log(isEnglish
                            ? count + " driver update(s) found. Use Install to apply."
                            : count + " Treiber-Update(s) gefunden. Mit „Installieren“ anwenden.");

                    await log("");
                    await log(isEnglish ? "Step 2: Optional vendor tools (winget)…" : "Schritt 2: Optionale Hersteller-Tools (winget)…");
                    await TryWingetGpuUpgradeAsync(log, isEnglish, ct);
                }

                if (installAfterSearch || installOnly)
                {
                    await log("");
                    await log(isEnglish
                        ? "Installing Windows driver updates… (admin may be required)"
                        : "Installiere Windows-Treiber-Updates… (Admin kann nötig sein)");
                    await InstallWindowsDriverUpdatesAsync(log, isEnglish, ct);
                }

                await log("");
                await log(isEnglish ? "In-app driver update finished." : "In-App Treiber-Update abgeschlossen.");
            }
            catch (OperationCanceledException)
            {
                await log(isEnglish ? "Driver update cancelled." : "Treiber-Update abgebrochen.");
            }
            finally
            {
                IsRunning = false;
                _cts?.Dispose();
                _cts = null;
            }
        }

        private static async Task<int> SearchWindowsDriverUpdatesAsync(Func<string, Task> log, bool en, CancellationToken ct)
        {
            const string script = @"
$ErrorActionPreference = 'SilentlyContinue'
try {
  $s = New-Object -ComObject Microsoft.Update.Session
  $searcher = $s.CreateUpdateSearcher()
  $r = $searcher.Search('IsInstalled=0 and Type=''Driver''')
  Write-Output ('COUNT:' + $r.Updates.Count)
  for ($i = 0; $i -lt $r.Updates.Count; $i++) {
    $u = $r.Updates.Item($i)
    Write-Output ('TITLE:' + $u.Title)
  }
} catch {
  Write-Output ('ERROR:' + $_.Exception.Message)
  exit 1
}
";
            (int code, string output) = await RunPowerShellAsync(script, ct);
            int count = 0;
            foreach (string line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                ct.ThrowIfCancellationRequested();
                if (line.StartsWith("COUNT:", StringComparison.OrdinalIgnoreCase))
                {
                    int.TryParse(line.Substring(6).Trim(), out count);
                    continue;
                }
                if (line.StartsWith("TITLE:", StringComparison.OrdinalIgnoreCase))
                    await log("• " + line.Substring(6).Trim());
                if (line.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase))
                    await log(en ? "Windows Update error: " + line.Substring(6) : "Windows-Update-Fehler: " + line.Substring(6));
            }

            if (code != 0 && count == 0)
                await log(en
                    ? "Windows Update search failed. Run Redline as administrator or use Windows Update manually."
                    : "Windows-Update-Suche fehlgeschlagen. Redline als Administrator starten oder Windows Update manuell öffnen.");
            return count;
        }

        private static async Task InstallWindowsDriverUpdatesAsync(Func<string, Task> log, bool en, CancellationToken ct)
        {
            const string script = @"
$ErrorActionPreference = 'Stop'
try {
  $s = New-Object -ComObject Microsoft.Update.Session
  $searcher = $s.CreateUpdateSearcher()
  $r = $searcher.Search('IsInstalled=0 and Type=''Driver''')
  if ($r.Updates.Count -eq 0) { Write-Output 'NONE'; exit 0 }
  $coll = New-Object -ComObject Microsoft.Update.UpdateColl
  for ($i = 0; $i -lt $r.Updates.Count; $i++) { [void]$coll.Add($r.Updates.Item($i)) }
  $dl = $s.CreateUpdateDownloader()
  $dl.Updates = $coll
  Write-Output 'DOWNLOADING'
  $dlResult = $dl.Download()
  Write-Output ('DLRESULT:' + $dlResult.ResultCode)
  $inst = $s.CreateUpdateInstaller()
  $inst.Updates = $coll
  Write-Output 'INSTALLING'
  $instResult = $inst.Install()
  Write-Output ('INSTRESULT:' + $instResult.ResultCode)
  Write-Output ('REBOOT:' + $instResult.RebootRequired)
} catch {
  Write-Output ('ERROR:' + $_.Exception.Message)
  exit 1
}
";
            (int code, string output) = await RunPowerShellAsync(script, ct);
            foreach (string line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                ct.ThrowIfCancellationRequested();
                if (line == "NONE")
                {
                    await log(en ? "Nothing to install." : "Nichts zu installieren.");
                    return;
                }
                if (line == "DOWNLOADING")
                    await log(en ? "Downloading updates…" : "Lade Updates herunter…");
                else if (line == "INSTALLING")
                    await log(en ? "Installing updates…" : "Installiere Updates…");
                else if (line.StartsWith("DLRESULT:", StringComparison.OrdinalIgnoreCase))
                    await log(en ? "Download result: " + line.Substring(9) : "Download-Ergebnis: " + line.Substring(9));
                else if (line.StartsWith("INSTRESULT:", StringComparison.OrdinalIgnoreCase))
                    await log(en ? "Install result: " + line.Substring(11) : "Install-Ergebnis: " + line.Substring(11));
                else if (line.StartsWith("REBOOT:", StringComparison.OrdinalIgnoreCase))
                    await log(en ? "Reboot required: " + line.Substring(7) : "Neustart nötig: " + line.Substring(7));
                else if (line.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase))
                    await log(en ? "Install error: " + line.Substring(6) : "Install-Fehler: " + line.Substring(6));
            }

            if (code != 0)
                await log(en
                    ? "Install failed. Try: Run Redline as administrator."
                    : "Installation fehlgeschlagen. Tipp: Redline als Administrator starten.");
        }

        private static async Task TryWingetGpuUpgradeAsync(Func<string, Task> log, bool en, CancellationToken ct)
        {
            string which = await RunCmdCaptureAsync("where.exe", "winget", ct);
            if (string.IsNullOrWhiteSpace(which) || which.Contains("INFO: Could not find", StringComparison.OrdinalIgnoreCase))
            {
                await log(en ? "winget not found – skipped." : "winget nicht gefunden – übersprungen.");
                return;
            }

            await log(en ? "Checking winget for GPU-related packages…" : "Prüfe winget auf GPU-Pakete…");
            (int code, string output) = await RunCmdCaptureAsyncFull("winget", "upgrade --disable-interactivity --accept-source-agreements --accept-package-agreements", ct, 120000);
            foreach (string line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("AMD", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("Intel", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("Graphics", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("Grafik", StringComparison.OrdinalIgnoreCase))
                    await log("winget: " + line.Trim());
            }

            if (code != 0)
                await log(en ? "winget finished with code " + code : "winget beendet mit Code " + code);
        }

        private static async Task<(int exitCode, string output)> RunPowerShellAsync(string script, CancellationToken ct)
        {
            string path = Path.Combine(Path.GetTempPath(), "redline-driver-ps-" + Guid.NewGuid().ToString("N") + ".ps1");
            try
            {
                await File.WriteAllTextAsync(path, script, Encoding.UTF8, ct);
                return await RunCmdCaptureAsyncFull("powershell.exe",
                    "-NoProfile -ExecutionPolicy Bypass -File \"" + path + "\"", ct, 180000);
            }
            finally
            {
                try { if (File.Exists(path)) File.Delete(path); } catch { }
            }
        }

        private static async Task<string> RunCmdCaptureAsync(string file, string args, CancellationToken ct)
        {
            (int _, string o) = await RunCmdCaptureAsyncFull(file, args, ct, 60000);
            return o;
        }

        private static async Task<(int exitCode, string output)> RunCmdCaptureAsyncFull(
            string file, string args, CancellationToken ct, int timeoutMs)
        {
            var sb = new StringBuilder();
            using Process p = new Process();
            p.StartInfo = new ProcessStartInfo
            {
                FileName = file,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            p.OutputDataReceived += (_, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
            p.ErrorDataReceived += (_, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            using var reg = ct.Register(() => { try { if (!p.HasExited) p.Kill(true); } catch { } });
            bool done = await Task.Run(() => p.WaitForExit(timeoutMs), ct);
            if (!done)
            {
                try { p.Kill(true); } catch { }
                throw new OperationCanceledException();
            }
            return (p.ExitCode, sb.ToString());
        }
    }
}
