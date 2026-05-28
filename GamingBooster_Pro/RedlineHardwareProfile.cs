using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;

namespace GamingBooster_Pro
{
    internal sealed class HardwareProfile
    {
        public string CpuName { get; set; } = "";
        public string GpuName { get; set; } = "";
        public string MotherboardManufacturer { get; set; } = "";
        public string MotherboardProduct { get; set; } = "";
        public string WindowsCaption { get; set; } = "";
    }

    internal sealed class DriverUpdateLink
    {
        public string Id { get; set; } = "";
        public string LabelDe { get; set; } = "";
        public string LabelEn { get; set; } = "";
        public string Url { get; set; } = "";
        public string ReasonDe { get; set; } = "";
        public string ReasonEn { get; set; } = "";
    }

    internal sealed class WingetDriverPackage
    {
        public string IdOrQuery { get; set; } = "";
        public bool UseExactId { get; set; }
        public string LabelDe { get; set; } = "";
        public string LabelEn { get; set; } = "";
    }

    internal static class RedlineHardwareProfile
    {
        public static HardwareProfile Detect(string cpu, string gpu, string windows)
        {
            HardwareProfile hp = new HardwareProfile
            {
                CpuName = cpu ?? "",
                GpuName = gpu ?? "",
                WindowsCaption = windows ?? ""
            };

            try
            {
                using ManagementObjectSearcher board = new ManagementObjectSearcher("SELECT Manufacturer, Product FROM Win32_BaseBoard");
                foreach (ManagementObject o in board.Get())
                {
                    hp.MotherboardManufacturer = (o["Manufacturer"]?.ToString() ?? "").Trim();
                    hp.MotherboardProduct = (o["Product"]?.ToString() ?? "").Trim();
                    break;
                }
            }
            catch { }

            return hp;
        }

        public static string GpuVendor(string gpuName)
        {
            string g = (gpuName ?? "").ToLowerInvariant();
            if (g.Contains("nvidia") || g.Contains("geforce") || g.Contains("rtx") || g.Contains("gtx"))
                return "NVIDIA";
            if (g.Contains("amd") || g.Contains("radeon") || g.Contains("rx "))
                return "AMD";
            if (g.Contains("intel") && (g.Contains("graphics") || g.Contains("uhd") || g.Contains("iris") || g.Contains("arc")))
                return "Intel";
            return "";
        }

        public static string CpuVendor(string cpuName)
        {
            string c = (cpuName ?? "").ToLowerInvariant();
            if (c.Contains("amd") || c.Contains("ryzen"))
                return "AMD";
            if (c.Contains("intel"))
                return "Intel";
            return "";
        }

        public static List<DriverUpdateLink> BuildSmartUpdateLinks(HardwareProfile hp, IEnumerable<string> driverStatuses)
        {
            List<DriverUpdateLink> links = new List<DriverUpdateLink>();
            HashSet<string> added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void Add(string id, string de, string en, string url, string rDe, string rEn)
            {
                if (string.IsNullOrWhiteSpace(url) || added.Contains(id))
                    return;
                added.Add(id);
                links.Add(new DriverUpdateLink { Id = id, LabelDe = de, LabelEn = en, Url = url, ReasonDe = rDe, ReasonEn = rEn });
            }

            string gpuV = GpuVendor(hp.GpuName);
            string cpuV = CpuVendor(hp.CpuName);
            string mb = (hp.MotherboardManufacturer + " " + hp.MotherboardProduct).ToLowerInvariant();

            if (gpuV == "NVIDIA")
                Add("nvidia", "NVIDIA Grafiktreiber", "NVIDIA graphics driver", "https://www.nvidia.com/Download/index.aspx",
                    "Erkannte NVIDIA GPU: " + hp.GpuName, "Detected NVIDIA GPU: " + hp.GpuName);
            else if (gpuV == "AMD")
                Add("amd-gpu", "AMD Grafiktreiber", "AMD graphics driver", "https://www.amd.com/en/support/download/drivers.html",
                    "Erkannte AMD GPU: " + hp.GpuName, "Detected AMD GPU: " + hp.GpuName);
            else if (gpuV == "Intel")
                Add("intel-gpu", "Intel Grafiktreiber", "Intel graphics driver", "https://www.intel.com/content/www/us/en/support/detect.html",
                    "Erkannte Intel GPU", "Detected Intel GPU");

            if (cpuV == "AMD")
                Add("amd-chipset", "AMD Chipsatz / Ryzen", "AMD chipset / Ryzen", "https://www.amd.com/en/support/chipsets/amd-socket-am5/am5",
                    "Erkannte AMD CPU: " + hp.CpuName, "Detected AMD CPU: " + hp.CpuName);

            if (cpuV == "Intel")
                Add("intel-cpu", "Intel Treiber & Support", "Intel drivers & support", "https://www.intel.com/content/www/us/en/support/detect.html",
                    "Erkannte Intel CPU", "Detected Intel CPU");

            if (mb.Contains("asus"))
                Add("mb-asus", "ASUS Mainboard Support", "ASUS motherboard support", "https://www.asus.com/support/download-center/",
                    "Mainboard: " + hp.MotherboardManufacturer + " " + hp.MotherboardProduct,
                    "Motherboard: " + hp.MotherboardManufacturer + " " + hp.MotherboardProduct);
            else if (mb.Contains("msi"))
                Add("mb-msi", "MSI Mainboard Support", "MSI motherboard support", "https://www.msi.com/support/download",
                    "Mainboard: " + hp.MotherboardManufacturer + " " + hp.MotherboardProduct,
                    "Motherboard: " + hp.MotherboardManufacturer + " " + hp.MotherboardProduct);
            else if (mb.Contains("gigabyte"))
                Add("mb-giga", "Gigabyte Mainboard Support", "Gigabyte motherboard support", "https://www.gigabyte.com/Support",
                    "Mainboard: " + hp.MotherboardManufacturer + " " + hp.MotherboardProduct,
                    "Motherboard: " + hp.MotherboardManufacturer + " " + hp.MotherboardProduct);
            else if (mb.Contains("asrock"))
                Add("mb-asrock", "ASRock Mainboard Support", "ASRock motherboard support", "https://www.asrock.com/support/index.asp",
                    "Mainboard: " + hp.MotherboardManufacturer + " " + hp.MotherboardProduct,
                    "Motherboard: " + hp.MotherboardManufacturer + " " + hp.MotherboardProduct);
            else if (!string.IsNullOrWhiteSpace(hp.MotherboardManufacturer))
                Add("mb-generic", "Mainboard Hersteller", "Motherboard vendor", "https://www.google.com/search?q=" + Uri.EscapeDataString(hp.MotherboardManufacturer + " " + hp.MotherboardProduct + " driver download"),
                    "Mainboard: " + hp.MotherboardManufacturer + " " + hp.MotherboardProduct,
                    "Motherboard: " + hp.MotherboardManufacturer + " " + hp.MotherboardProduct);

            Add("realtek", "Realtek Audio/LAN", "Realtek audio/LAN", "https://www.realtek.com/Download/List?cate_id=584",
                "Audio/Netzwerk Treiber", "Audio/network drivers");

            Add("devmgr", "Geräte-Manager", "Device Manager", "devmgmt.msc",
                "Einzelne Geräte mit Fehlercode prüfen", "Check devices with error codes");

            return links;
        }

        /// <summary>Only packages matching detected GPU/CPU — no NVIDIA on AMD systems, etc.</summary>
        public static List<WingetDriverPackage> BuildWingetPackagesForHardware(HardwareProfile hp)
        {
            List<WingetDriverPackage> packages = new List<WingetDriverPackage>();
            string gpuV = GpuVendor(hp.GpuName);
            string cpuV = CpuVendor(hp.CpuName);

            if (gpuV == "NVIDIA")
            {
                packages.Add(new WingetDriverPackage
                {
                    IdOrQuery = "NVIDIA.GraphicsDriver",
                    UseExactId = true,
                    LabelDe = "NVIDIA Grafiktreiber",
                    LabelEn = "NVIDIA graphics driver"
                });
                packages.Add(new WingetDriverPackage
                {
                    IdOrQuery = "NVIDIA.App",
                    UseExactId = true,
                    LabelDe = "NVIDIA App",
                    LabelEn = "NVIDIA App"
                });
            }
            else if (gpuV == "AMD")
            {
                packages.Add(new WingetDriverPackage
                {
                    IdOrQuery = "AMD",
                    UseExactId = false,
                    LabelDe = "AMD Grafik / Software",
                    LabelEn = "AMD graphics / software"
                });
            }
            else if (gpuV == "Intel")
            {
                packages.Add(new WingetDriverPackage
                {
                    IdOrQuery = "Intel Graphics",
                    UseExactId = false,
                    LabelDe = "Intel Grafiktreiber",
                    LabelEn = "Intel graphics driver"
                });
            }

            if (cpuV == "AMD")
            {
                packages.Add(new WingetDriverPackage
                {
                    IdOrQuery = "AMD.Chipset.Software",
                    UseExactId = true,
                    LabelDe = "AMD Chipsatz (Ryzen)",
                    LabelEn = "AMD chipset (Ryzen)"
                });
            }
            else if (cpuV == "Intel" && gpuV != "Intel")
            {
                packages.Add(new WingetDriverPackage
                {
                    IdOrQuery = "Intel",
                    UseExactId = false,
                    LabelDe = "Intel Treiber & Support",
                    LabelEn = "Intel drivers & support"
                });
            }

            packages.Add(new WingetDriverPackage
            {
                IdOrQuery = "Realtek",
                UseExactId = false,
                LabelDe = "Realtek Audio/LAN (optional)",
                LabelEn = "Realtek audio/LAN (optional)"
            });

            return packages;
        }

        public static string FormatHardwareSummary(HardwareProfile hp, bool english)
        {
            string gpuV = GpuVendor(hp.GpuName);
            string cpuV = CpuVendor(hp.CpuName);
            if (english)
                return "GPU: " + hp.GpuName + (string.IsNullOrEmpty(gpuV) ? "" : " [" + gpuV + "]")
                    + " | CPU: " + hp.CpuName + (string.IsNullOrEmpty(cpuV) ? "" : " [" + cpuV + "]");
            return "GPU: " + hp.GpuName + (string.IsNullOrEmpty(gpuV) ? "" : " [" + gpuV + "]")
                + " | CPU: " + hp.CpuName + (string.IsNullOrEmpty(cpuV) ? "" : " [" + cpuV + "]");
        }
    }
}
