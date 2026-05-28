using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            HardwareProfile hardware,
            bool installPackages,
            Func<string, Task> log,
            bool isEnglish,
            CancellationToken externalCt = default)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(externalCt);
            CancellationToken ct = _cts.Token;
            IsRunning = true;
            try
            {
                await log("═══════════════════════════════════════");
                await log(isEnglish ? "Redline Driver Update (winget)" : "Redline Treiber-Update (winget)");
                await log(RedlineHardwareProfile.FormatHardwareSummary(hardware, isEnglish));
                string gpuV = RedlineHardwareProfile.GpuVendor(hardware.GpuName);
                if (string.IsNullOrEmpty(gpuV))
                    await log(isEnglish
                        ? "⚠ GPU vendor unknown — trying generic packages only."
                        : "⚠ GPU-Hersteller unbekannt — nur generische Pakete.");
                else
                    await log(isEnglish
                        ? "Installing only packages for: " + gpuV + " GPU + your CPU vendor."
                        : "Installiert nur Pakete für: " + gpuV + " GPU + deinen CPU-Hersteller.");
                await log(isEnglish
                    ? "Windows Update is not used — use vendor links if needed."
                    : "Windows Update wird nicht genutzt — bei Bedarf Hersteller-Links.");
                await log("");

                if (!installPackages)
                {
                    await log(isEnglish ? "Preview — packages for your PC:" : "Vorschau — Pakete für deinen PC:");
                    foreach (WingetDriverPackage pkg in RedlineHardwareProfile.BuildWingetPackagesForHardware(hardware))
                        await log("  • " + (isEnglish ? pkg.LabelEn : pkg.LabelDe));
                    await log(isEnglish ? "Click Install to run winget." : "„Installieren“ startet winget.");
                    return;
                }

                await TryWingetHardwareUpgradeAsync(hardware, log, isEnglish, ct);
                await log("");
                await log(isEnglish ? "Driver update finished." : "Treiber-Update abgeschlossen.");
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

        public async Task<bool> InstallSingleByDeviceHintAsync(
            string deviceHint,
            Func<string, Task> log,
            bool isEnglish,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(deviceHint))
                return false;

            await log(isEnglish ? "Opening vendor page…" : "Öffne Hersteller-Seite…");
            await OpenVendorUpdateForDeviceAsync(deviceHint.Trim(), log, isEnglish, ct);
            RedlineDriverStatus.MarkRecentlyUpdated(deviceHint.Trim());
            return false;
        }

        private static async Task OpenVendorUpdateForDeviceAsync(string deviceHint, Func<string, Task> log, bool en, CancellationToken ct)
        {
            string d = deviceHint.ToLowerInvariant();
            string url = "https://www.google.com/search?q=" + Uri.EscapeDataString(deviceHint + " driver download");
            if (d.Contains("nvidia") || d.Contains("geforce"))
                url = "https://www.nvidia.com/Download/index.aspx";
            else if (d.Contains("amd") || d.Contains("radeon"))
                url = "https://www.amd.com/en/support/download/drivers.html";
            else if (d.Contains("intel"))
                url = "https://www.intel.com/content/www/us/en/support/detect.html";
            else if (d.Contains("realtek"))
                url = "https://www.realtek.com/Download/List?cate_id=584";

            ct.ThrowIfCancellationRequested();
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                await log(en ? "Opened: " + url : "Geöffnet: " + url);
            }
            catch (Exception ex)
            {
                await log(en ? "Could not open: " + ex.Message : "Konnte nicht öffnen: " + ex.Message);
            }
        }

        private static async Task TryWingetHardwareUpgradeAsync(
            HardwareProfile hardware,
            Func<string, Task> log,
            bool en,
            CancellationToken ct)
        {
            string which = await RunCmdCaptureAsync("where.exe", "winget", ct);
            if (string.IsNullOrWhiteSpace(which) || which.Contains("INFO: Could not find", StringComparison.OrdinalIgnoreCase))
            {
                await log(en ? "❌ winget not found — install App Installer from Microsoft Store." : "❌ winget nicht gefunden — App Installer aus dem Store installieren.");
                return;
            }

            List<WingetDriverPackage> plan = RedlineHardwareProfile.BuildWingetPackagesForHardware(hardware);
            string gpuV = RedlineHardwareProfile.GpuVendor(hardware.GpuName);
            if (gpuV == "NVIDIA")
                await log(en ? "Skipping AMD/Intel GPU packages." : "Überspringe AMD/Intel GPU-Pakete.");
            else if (gpuV == "AMD")
                await log(en ? "Skipping NVIDIA packages." : "Überspringe NVIDIA-Pakete.");
            else if (gpuV == "Intel")
                await log(en ? "Skipping NVIDIA/AMD GPU packages." : "Überspringe NVIDIA/AMD GPU-Pakete.");

            string wingetArgs = "--disable-interactivity --accept-source-agreements --accept-package-agreements -e --silent";
            bool any = false;

            foreach (WingetDriverPackage pkg in plan)
            {
                ct.ThrowIfCancellationRequested();
                string label = en ? pkg.LabelEn : pkg.LabelDe;
                string target = pkg.IdOrQuery;
                string args = pkg.UseExactId
                    ? "upgrade --id \"" + target + "\" " + wingetArgs
                    : "upgrade --query \"" + target + "\" " + wingetArgs;

                await log(en
                    ? "▸ " + label + " (" + (pkg.UseExactId ? "ID: " : "") + target + ")"
                    : "▸ " + label + " (" + (pkg.UseExactId ? "ID: " : "") + target + ")");

                (int code, string output) = await RunCmdCaptureAsyncFull("winget", args, ct, 300000);

                bool matched = false;
                foreach (string line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string t = line.Trim();
                    if (t.Length < 3)
                        continue;
                    if (t.Contains("Es wurden keine", StringComparison.OrdinalIgnoreCase)
                        || t.Contains("No applicable", StringComparison.OrdinalIgnoreCase)
                        || t.Contains("no upgrades", StringComparison.OrdinalIgnoreCase)
                        || t.Contains("nicht installiert", StringComparison.OrdinalIgnoreCase)
                        || t.Contains("not installed", StringComparison.OrdinalIgnoreCase))
                    {
                        await log(en ? "  · not installed / nothing to upgrade" : "  · nicht installiert / nichts zu updaten");
                        matched = true;
                        break;
                    }
                    if (t.Contains("Erfolgreich installiert", StringComparison.OrdinalIgnoreCase)
                        || t.Contains("Successfully installed", StringComparison.OrdinalIgnoreCase)
                        || t.Contains("upgraded", StringComparison.OrdinalIgnoreCase))
                    {
                        await log("  ✅ " + t);
                        matched = true;
                        any = true;
                    }
                }

                if (!matched && code == 0)
                    await log(en ? "  · already current or not in winget" : "  · bereits aktuell oder nicht in winget");
                else if (code != 0 && !matched)
                    await log(en ? "  ⚠ winget exit " + code : "  ⚠ winget Code " + code);
            }

            if (!any)
                await log(en
                    ? "No packages upgraded (may already be current)."
                    : "Keine Pakete aktualisiert (evtl. schon aktuell).");
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
