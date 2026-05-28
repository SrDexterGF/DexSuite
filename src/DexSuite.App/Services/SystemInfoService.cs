using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using DexSuite.App.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace DexSuite.App.Services;

/// <summary>
/// Recopila especificaciones de hardware usando WMI, Registro y DriveInfo.
/// Las consultas WMI se ejecutan en un hilo de pool para no bloquear la UI.
/// </summary>
public sealed class SystemInfoService : ISystemInfoService
{
    private readonly ILogger<SystemInfoService> _logger;

    // P/Invoke para RAM total/disponible
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MemStatusEx buffer);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private sealed class MemStatusEx
    {
        public uint  dwLength       = (uint)Marshal.SizeOf(typeof(MemStatusEx));
        public uint  dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    public SystemInfoService(ILogger<SystemInfoService> logger)
    {
        _logger = logger;
    }

    public Task<SystemInfo> GetSystemInfoAsync()
        => Task.Run(Collect);

    private SystemInfo Collect()
    {
        var cpuName    = "Desconocido";
        int cpuCores   = 0;
        int cpuLogical = Environment.ProcessorCount;
        int cpuMhz     = 0;

        var gpuName = "Desconocido";
        var osName  = Environment.OSVersion.ToString();
        var osBuild = "";

        // ── CPU via WMI ──────────────────────────────────────────────────
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, NumberOfCores, MaxClockSpeed FROM Win32_Processor");
            foreach (ManagementObject obj in searcher.Get())
            {
                cpuName  = obj["Name"]?.ToString()?.Trim() ?? cpuName;
                cpuCores = Convert.ToInt32(obj["NumberOfCores"]);
                cpuMhz   = Convert.ToInt32(obj["MaxClockSpeed"]);
                break; // solo el primer procesador físico
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WMI CPU query failed; usando fallback de registro");
            // Fallback: registro
            try
            {
                var key = Registry.LocalMachine
                    .OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                cpuName = key?.GetValue("ProcessorNameString")?.ToString()?.Trim() ?? cpuName;
                cpuMhz  = Convert.ToInt32(key?.GetValue("~MHz") ?? 0);
            }
            catch { /* ignorar */ }
        }

        // ── GPU via WMI ──────────────────────────────────────────────────
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name FROM Win32_VideoController WHERE AdapterCompatibility IS NOT NULL");
            foreach (ManagementObject obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(name))
                {
                    gpuName = name;
                    break; // primera GPU (normalmente la discreta)
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WMI GPU query failed");
        }

        // ── SO via WMI ───────────────────────────────────────────────────
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Caption, BuildNumber, Version FROM Win32_OperatingSystem");
            foreach (ManagementObject obj in searcher.Get())
            {
                osName  = obj["Caption"]?.ToString()?.Trim() ?? osName;
                osBuild = obj["BuildNumber"]?.ToString() ?? "";
                break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WMI OS query failed; usando fallback de registro");
            try
            {
                var key = Registry.LocalMachine
                    .OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
                var product = key?.GetValue("ProductName")?.ToString() ?? "";
                var display = key?.GetValue("DisplayVersion")?.ToString() ?? "";
                osName  = string.IsNullOrEmpty(display) ? product : $"{product} {display}";
                osBuild = key?.GetValue("CurrentBuildNumber")?.ToString() ?? "";
            }
            catch { /* ignorar */ }
        }

        // ── RAM via P/Invoke ─────────────────────────────────────────────
        int ramTotalGb = 0, ramAvailGb = 0;
        try
        {
            var mem = new MemStatusEx();
            if (GlobalMemoryStatusEx(mem))
            {
                ramTotalGb = (int)(mem.ullTotalPhys / 1_073_741_824UL);
                ramAvailGb = (int)(mem.ullAvailPhys / 1_073_741_824UL);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GlobalMemoryStatusEx failed");
        }

        // ── Discos via DriveInfo ─────────────────────────────────────────
        var disks = new List<DiskInfo>();
        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.DriveType != DriveType.Fixed) continue;
                try
                {
                    disks.Add(new DiskInfo(
                        drive.Name.TrimEnd('\\'),
                        drive.VolumeLabel,
                        drive.TotalSize   / 1_073_741_824L,
                        drive.TotalFreeSpace / 1_073_741_824L,
                        drive.DriveFormat));
                }
                catch { /* unidad no lista, ignorar */ }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DriveInfo enumeration failed");
        }

        return new SystemInfo(
            CpuName:        cpuName,
            CpuCores:       cpuCores > 0 ? cpuCores : cpuLogical,
            CpuLogical:     cpuLogical,
            CpuSpeedMhz:    cpuMhz,
            RamTotalGb:     ramTotalGb,
            RamAvailableGb: ramAvailGb,
            GpuName:        gpuName,
            OsName:         osName,
            OsBuild:        osBuild,
            Disks:          disks);
    }
}
