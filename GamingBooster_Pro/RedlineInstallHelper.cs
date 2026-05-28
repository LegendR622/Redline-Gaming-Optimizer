using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace GamingBooster_Pro
{
    internal static class RedlineInstallHelper
    {
        private const string UninstallKeyName = "A7B3C9E1-4F2D-4A8B-9C0E-REDLINE-GAMING-01_is1";
        public const string AppMutexName = "RedlineGamingOptimizerMutex";

        public static bool TryGetInstalledLocation(out string installDir)
        {
            installDir = "";
            try
            {
                foreach (RegistryHive hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
                {
                    using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
                    using RegistryKey? key = baseKey.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + UninstallKeyName);
                    string? loc = key?.GetValue("InstallLocation") as string;
                    if (!string.IsNullOrWhiteSpace(loc) && Directory.Exists(loc))
                    {
                        installDir = loc.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        return true;
                    }
                }
            }
            catch { }

            return false;
        }

        public static bool IsSetupInstalled() => TryGetInstalledLocation(out _);

        public static string? TryGetInstalledVersion()
        {
            try
            {
                foreach (RegistryHive hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
                {
                    using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
                    using RegistryKey? key = baseKey.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + UninstallKeyName);
                    string? raw = key?.GetValue("DisplayVersion") as string;
                    if (string.IsNullOrWhiteSpace(raw))
                        continue;
                    return NormalizeVersionLabel(raw);
                }
            }
            catch { }

            return null;
        }

        private static string NormalizeVersionLabel(string raw)
        {
            string cleaned = new string(raw.Where(c => char.IsDigit(c) || c == '.').ToArray());
            if (string.IsNullOrWhiteSpace(cleaned))
                return raw.Trim();
            string[] parts = cleaned.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                return parts[0] + "." + parts[1];
            return cleaned;
        }

        public static string BuildSilentInstallerArgs()
        {
            string args = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS";
            if (TryGetInstalledLocation(out string dir))
                return args + " /DIR=\"" + dir + "\"";
            if (IsUnderProgramFiles(AppContext.BaseDirectory))
                return args + " /DIR=\"" + AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + "\"";
            return args;
        }

        private static bool IsUnderProgramFiles(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;
            string p = path.ToLowerInvariant();
            return p.Contains(@"\program files\redline gaming optimizer")
                || p.Contains(@"\program files (x86)\redline gaming optimizer");
        }

        /// <summary>
        /// Starts the Inno Setup installer (UAC is requested by the setup EXE, not via runas on the parent app).
        /// </summary>
        public static bool TryLaunchInstaller(string installerPath, string arguments, out string? errorMessage)
        {
            errorMessage = null;
            if (string.IsNullOrWhiteSpace(installerPath) || !File.Exists(installerPath))
            {
                errorMessage = "Installer nicht gefunden: " + installerPath;
                return false;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = installerPath,
                    Arguments = arguments,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(installerPath) ?? ""
                });
                return true;
            }
            catch (Win32Exception ex)
            {
                errorMessage = ex.NativeErrorCode == 1223
                    ? "Windows-Bestätigung (UAC) abgebrochen."
                    : ex.Message;
                return false;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }
    }
}
