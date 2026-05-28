using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace GamingBooster_Pro
{
    /// <summary>
    /// Entwickler-Pro nur auf autorisierten PCs (Hardware-Fingerabdruck + optional Dev-Key).
    /// </summary>
    internal static class RedlineDevAuth
    {
        /// <summary>SHA-256 von Domain\User|Machine|MachineGuid – Tobias Entwickler-PC.</summary>
        private static readonly string[] AuthorizedDeveloperMachineHashes =
        {
            "9722b041a0f1b5d685993bc3d2bff519623850d9814b4632a5c858bca22dd294"
        };

        public static bool IsAuthorizedDeveloperMachine()
        {
            try
            {
                string hash = ComputeMachineFingerprintHash();
                return AuthorizedDeveloperMachineHashes.Any(h =>
                    string.Equals(h, hash, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }

        public static string GetMachineLabel()
        {
            try
            {
                return Environment.MachineName + " · " + Environment.UserName;
            }
            catch
            {
                return "PC";
            }
        }

        public static string ComputeMachineFingerprintHash()
        {
            string guid = TryGetMachineGuid();
            string raw = Environment.UserDomainName + "\\" + Environment.UserName + "|"
                + Environment.MachineName + "|" + guid;
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static string TryGetMachineGuid()
        {
            try
            {
                using RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
                object? val = key?.GetValue("MachineGuid");
                return val?.ToString() ?? "";
            }
            catch
            {
                return "";
            }
        }
    }
}
