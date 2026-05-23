using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using DexSuite.App.Models;
using Microsoft.Win32;

namespace DexSuite.App.Services;

/// <summary>
/// Mide el estado del equipo en 8 dimensiones y devuelve un score 0-100 por cada una.
/// Total = promedio simple. Las medidas son una foto instantanea, asi que dos analisis
/// seguidos pueden variar unos puntos: lo importante es comparar antes/despues.
///
/// Categorias (peso igual):
///   1) CPU       - % CPU en uso, medido con GetSystemTimes en dos muestras.
///   2) Memoria   - % de RAM en uso, GlobalMemoryStatusEx.
///   3) GPU       - % uso GPU, PerformanceCounter "GPU Engine" sumando todas las instancias.
///   4) Disco C   - % libre del disco del sistema, DriveInfo.
///   5) Procesos  - Process.GetProcesses().Length.
///   6) Inicio    - Cuantas apps arrancan con Windows (HKLM + HKCU \Run).
///   7) Temp      - Tamano del directorio TEMP del usuario.
///   8) Red       - RTT a 1.1.1.1 (Cloudflare).
///
/// Todo es lectura: no modifica nada del sistema.
/// </summary>
public sealed class PerformanceAnalyzer : IPerformanceAnalyzer
{
    public async Task<PerformanceScore> AnalyzeAsync(CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var cats = new List<PerformanceCategoryScore>
            {
                MeasureCpu(ct),
                MeasureRam(),
                MeasureGpu(ct),
                MeasureDisk(),
                MeasureProcesses(),
                MeasureStartup(),
                MeasureTemp(ct),
                MeasureNetwork(ct),
            };

            var total = (int)Math.Round(cats.Average(c => c.Score));
            return new PerformanceScore(total, cats, DateTime.Now);
        }, ct);
    }

    // ---- CPU ---------------------------------------------------------------

    private static PerformanceCategoryScore MeasureCpu(CancellationToken ct)
    {
        if (!GetSystemTimes(out var idle1, out var kernel1, out var user1))
            return new PerformanceCategoryScore("CPU", 50, "No se pudo medir");

        Task.Delay(500, ct).Wait(ct);

        if (!GetSystemTimes(out var idle2, out var kernel2, out var user2))
            return new PerformanceCategoryScore("CPU", 50, "No se pudo medir");

        var idleDiff = FileTimeToUInt64(idle2) - FileTimeToUInt64(idle1);
        var kernelDiff = FileTimeToUInt64(kernel2) - FileTimeToUInt64(kernel1);
        var userDiff = FileTimeToUInt64(user2) - FileTimeToUInt64(user1);
        var totalDiff = kernelDiff + userDiff;

        if (totalDiff == 0)
            return new PerformanceCategoryScore("CPU", 100, "0% en uso");

        var cpuUsage = (1.0 - (double)idleDiff / totalDiff) * 100.0;
        cpuUsage = Math.Clamp(cpuUsage, 0, 100);
        var score = (int)Math.Round(100 - cpuUsage);
        return new PerformanceCategoryScore("CPU", score, $"{cpuUsage:0}% en uso");
    }

    // ---- Memoria -----------------------------------------------------------

    private static PerformanceCategoryScore MeasureRam()
    {
        var status = new MEMORYSTATUSEX();
        if (!GlobalMemoryStatusEx(status))
            return new PerformanceCategoryScore("Memoria", 50, "No se pudo medir");

        var load = (int)status.dwMemoryLoad;
        var totalGb = status.ullTotalPhys / 1024.0 / 1024.0 / 1024.0;
        var availGb = status.ullAvailPhys / 1024.0 / 1024.0 / 1024.0;
        var score = Math.Clamp(100 - load, 0, 100);
        return new PerformanceCategoryScore(
            "Memoria", score,
            $"{load}% en uso ({availGb:0.0} GB libres de {totalGb:0.0} GB)");
    }

    // ---- GPU ---------------------------------------------------------------

    private static PerformanceCategoryScore MeasureGpu(CancellationToken ct)
    {
        try
        {
            // La categoria "GPU Engine" tiene una instancia por cada motor de cada GPU
            // (3D, VideoEncode, VideoDecode, Copy, ...). Sumamos todas las instancias
            // del contador "Utilization Percentage" y nos quedamos con el maximo.
            if (!PerformanceCounterCategory.Exists("GPU Engine"))
                return new PerformanceCategoryScore("GPU", 90, "No detectada (sin contador)");

            var category = new PerformanceCounterCategory("GPU Engine");
            var instances = category.GetInstanceNames();
            if (instances.Length == 0)
                return new PerformanceCategoryScore("GPU", 90, "No detectada (sin instancias)");

            var counters = new List<PerformanceCounter>();
            foreach (var inst in instances)
            {
                try
                {
                    var c = new PerformanceCounter("GPU Engine", "Utilization Percentage", inst, readOnly: true);
                    c.NextValue(); // primera lectura suele dar 0
                    counters.Add(c);
                }
                catch { /* instancia con permisos raros */ }
            }

            if (counters.Count == 0)
                return new PerformanceCategoryScore("GPU", 90, "No medible");

            Task.Delay(500, ct).Wait(ct);

            float total = 0;
            foreach (var c in counters)
            {
                try { total += c.NextValue(); } catch { }
            }

            // Cierro los counters para liberar handles.
            foreach (var c in counters) c.Dispose();

            // total puede superar 100 si varios motores estan al maximo a la vez,
            // pero como "uso de GPU" lo cap al 100.
            var usage = Math.Clamp(total, 0, 100);
            var score = (int)Math.Round(100 - usage);
            return new PerformanceCategoryScore("GPU", score, $"{usage:0}% en uso");
        }
        catch (Exception ex)
        {
            return new PerformanceCategoryScore("GPU", 50, $"No medible: {ex.Message}");
        }
    }

    // ---- Disco -------------------------------------------------------------

    private static PerformanceCategoryScore MeasureDisk()
    {
        try
        {
            var sys = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
            var drive = new DriveInfo(sys);
            if (!drive.IsReady)
                return new PerformanceCategoryScore("Disco", 50, "Disco no listo");

            var totalGb = drive.TotalSize / 1024.0 / 1024.0 / 1024.0;
            var freeGb = drive.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0;
            var freePct = freeGb / totalGb * 100.0;

            int score;
            if (freePct >= 50) score = 100;
            else if (freePct <= 10) score = 0;
            else score = (int)Math.Round((freePct - 10) / 40.0 * 100);

            return new PerformanceCategoryScore(
                "Disco", score,
                $"{freePct:0}% libre ({freeGb:0.0} GB de {totalGb:0.0} GB) en {drive.Name}");
        }
        catch (Exception ex)
        {
            return new PerformanceCategoryScore("Disco", 50, $"Error: {ex.Message}");
        }
    }

    // ---- Procesos ----------------------------------------------------------

    private static PerformanceCategoryScore MeasureProcesses()
    {
        try
        {
            var count = Process.GetProcesses().Length;
            int score;
            if (count <= 80) score = 100;
            else if (count >= 300) score = 0;
            else score = (int)Math.Round(100 - (count - 80) / 220.0 * 100);

            return new PerformanceCategoryScore("Procesos", score, $"{count} procesos activos");
        }
        catch (Exception ex)
        {
            return new PerformanceCategoryScore("Procesos", 50, $"Error: {ex.Message}");
        }
    }

    // ---- Apps al inicio ----------------------------------------------------

    private static PerformanceCategoryScore MeasureStartup()
    {
        try
        {
            var count = CountStartupEntries();
            int score;
            if (count <= 8) score = 100;
            else if (count >= 30) score = 0;
            else score = (int)Math.Round(100 - (count - 8) / 22.0 * 100);

            return new PerformanceCategoryScore("Inicio", score, $"{count} apps arrancan con Windows");
        }
        catch (Exception ex)
        {
            return new PerformanceCategoryScore("Inicio", 50, $"Error: {ex.Message}");
        }
    }

    private static int CountStartupEntries()
    {
        var paths = new (RegistryHive Hive, string Path)[]
        {
            (RegistryHive.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run"),
            (RegistryHive.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\RunOnce"),
            (RegistryHive.CurrentUser,  @"Software\Microsoft\Windows\CurrentVersion\Run"),
            (RegistryHive.CurrentUser,  @"Software\Microsoft\Windows\CurrentVersion\RunOnce"),
        };

        var total = 0;
        foreach (var (hive, path) in paths)
        {
            try
            {
                using var root = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
                using var key = root.OpenSubKey(path);
                if (key is null) continue;
                total += key.GetValueNames().Length;
            }
            catch { /* permisos */ }
        }
        return total;
    }

    // ---- Temp --------------------------------------------------------------

    private static PerformanceCategoryScore MeasureTemp(CancellationToken ct)
    {
        try
        {
            var tempPath = Path.GetTempPath();
            var dir = new DirectoryInfo(tempPath);
            if (!dir.Exists)
                return new PerformanceCategoryScore("Temp", 100, "Sin temporales");

            long bytes = 0;
            try
            {
                foreach (var f in dir.EnumerateFiles("*", SearchOption.AllDirectories))
                {
                    ct.ThrowIfCancellationRequested();
                    try { bytes += f.Length; } catch { /* bloqueado */ }
                }
            }
            catch (UnauthorizedAccessException) { /* subdirs sin permiso */ }
            catch (OperationCanceledException) { throw; }
            catch (DirectoryNotFoundException) { /* race */ }

            var mb = bytes / 1024.0 / 1024.0;
            int score;
            if (mb <= 0) score = 100;
            else if (mb >= 2000) score = 0;
            else score = (int)Math.Round(100 - mb / 2000.0 * 100);

            string detail = mb >= 1024
                ? $"{mb / 1024.0:0.0} GB en archivos temporales"
                : $"{mb:0} MB en archivos temporales";

            return new PerformanceCategoryScore("Temp", score, detail);
        }
        catch (Exception ex)
        {
            return new PerformanceCategoryScore("Temp", 50, $"Error: {ex.Message}");
        }
    }

    // ---- Red (ping Cloudflare) --------------------------------------------

    private static PerformanceCategoryScore MeasureNetwork(CancellationToken ct)
    {
        try
        {
            using var ping = new Ping();
            var reply = ping.Send("1.1.1.1", timeout: 1500);
            if (reply.Status != IPStatus.Success)
                return new PerformanceCategoryScore("Red", 30, $"No responde ({reply.Status})");

            var rtt = reply.RoundtripTime;
            // <=20ms = 100, >=300ms = 0, lineal en medio.
            int score;
            if (rtt <= 20) score = 100;
            else if (rtt >= 300) score = 0;
            else score = (int)Math.Round(100 - (rtt - 20) / 280.0 * 100);

            return new PerformanceCategoryScore("Red", score, $"{rtt} ms hasta 1.1.1.1");
        }
        catch (Exception ex)
        {
            return new PerformanceCategoryScore("Red", 50, $"No medible: {ex.Message}");
        }
    }

    // ---- P/Invoke ----------------------------------------------------------

    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME
    {
        public uint dwLowDateTime;
        public uint dwHighDateTime;
    }

    private static ulong FileTimeToUInt64(FILETIME ft) =>
        ((ulong)ft.dwHighDateTime << 32) | ft.dwLowDateTime;

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(
        out FILETIME lpIdleTime,
        out FILETIME lpKernelTime,
        out FILETIME lpUserTime);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private sealed class MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
        public MEMORYSTATUSEX() { dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX)); }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);
}
