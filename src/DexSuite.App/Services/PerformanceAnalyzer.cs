using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using DexSuite.App.Models;
using Microsoft.Win32;

namespace DexSuite.App.Services;

/// <summary>
/// Mide el rendimiento del equipo en 8 dimensiones y devuelve un score 0-100 por cada una.
///
/// ESTABILIDAD (F5.6):
///   Las métricas volátiles (CPU, GPU, Red) se miden con 3 muestras independientes
///   espaciadas 600 ms y se usa la MEDIANA. Esto elimina picos puntuales del SO,
///   antivirus, etc. y hace que el "Antes / Después" sea significativo.
///
///   CPU, GPU y Red se ejecutan en paralelo para que el tiempo total sea ≈ 2 s
///   (el máximo de los tres) en lugar de suma de todos.
///
/// Categorías y pesos:
///   1) CPU       – % CPU en uso  (mediana de 3 muestras × 600 ms)
///   2) Memoria   – % RAM en uso  (single snapshot — muy estable)
///   3) GPU       – % GPU en uso  (mediana de 3 muestras × 600 ms)
///   4) Disco C   – % libre       (single snapshot — muy estable)
///   5) Procesos  – nº procesos   (single snapshot — moderadamente estable)
///   6) Inicio    – apps startup  (single snapshot — muy estable)
///   7) Temp      – MB en %TEMP% (single snapshot — estable)
///   8) Red       – RTT ping      (mediana de 3 pings)
///
/// Todo es lectura: no modifica nada del sistema.
/// </summary>
public sealed class PerformanceAnalyzer : IPerformanceAnalyzer
{
    /// <summary>Número de muestras para métricas volátiles (CPU, GPU, Red).</summary>
    private const int Samples = 3;

    /// <summary>Ventana de medición por muestra (ms).</summary>
    private const int SampleMs = 600;

    public async Task<PerformanceScore> AnalyzeAsync(CancellationToken ct = default)
    {
        // Métricas volátiles: CPU, GPU y Red se miden en PARALELO.
        // Cada una toma Samples × SampleMs = 1.8 s internamente,
        // pero como corren a la vez el tiempo total es ≈ 1.8 s + overhead.
        var cpuTask     = Task.Run(() => MeasureCpuMultiSample(ct), ct);
        var gpuTask     = Task.Run(() => MeasureGpuMultiSample(ct), ct);
        var networkTask = Task.Run(() => MeasureNetworkMultiSample(ct), ct);

        // Métricas estables: se miden directamente (rápido).
        var ram       = MeasureRam();
        var disk      = MeasureDisk();
        var processes = MeasureProcesses();
        var startup   = MeasureStartup();
        var temp      = await Task.Run(() => MeasureTemp(ct), ct);

        // Esperamos las tres tareas volátiles en paralelo.
        await Task.WhenAll(cpuTask, gpuTask, networkTask).ConfigureAwait(false);

        var cats = new List<PerformanceCategoryScore>
        {
            cpuTask.Result,
            ram,
            gpuTask.Result,
            disk,
            processes,
            startup,
            temp,
            networkTask.Result,
        };

        var total = (int)Math.Round(cats.Average(c => c.Score));
        return new PerformanceScore(total, cats, DateTime.Now);
    }

    // ── CPU (multi-muestra, mediana) ─────────────────────────────────────────

    private static PerformanceCategoryScore MeasureCpuMultiSample(CancellationToken ct)
    {
        var samples = new List<double>(Samples);

        for (int i = 0; i < Samples; i++)
        {
            ct.ThrowIfCancellationRequested();

            if (!GetSystemTimes(out var idle1, out var kernel1, out var user1))
            {
                samples.Add(50.0); // fallback neutro
                continue;
            }

            Task.Delay(SampleMs, ct).Wait(ct);

            if (!GetSystemTimes(out var idle2, out var kernel2, out var user2))
            {
                samples.Add(50.0);
                continue;
            }

            var idleDiff   = ToUInt64(idle2)   - ToUInt64(idle1);
            var kernelDiff = ToUInt64(kernel2) - ToUInt64(kernel1);
            var userDiff   = ToUInt64(user2)   - ToUInt64(user1);
            var totalDiff  = kernelDiff + userDiff;

            var usage = totalDiff == 0 ? 0.0 : (1.0 - (double)idleDiff / totalDiff) * 100.0;
            samples.Add(Math.Clamp(usage, 0.0, 100.0));
        }

        var medianUsage = Median(samples);
        var score = (int)Math.Round(100 - medianUsage);
        return new PerformanceCategoryScore("CPU", score, $"{medianUsage:0}% en uso");
    }

    // ── GPU (multi-muestra, mediana) ─────────────────────────────────────────

    private static PerformanceCategoryScore MeasureGpuMultiSample(CancellationToken ct)
    {
        try
        {
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
                    c.NextValue(); // lectura inicial siempre da 0; descartada.
                    counters.Add(c);
                }
                catch { /* instancia sin permisos */ }
            }

            if (counters.Count == 0)
                return new PerformanceCategoryScore("GPU", 90, "No medible");

            // Warm-up: un intervalo inicial para que los contadores acumulen.
            Task.Delay(SampleMs, ct).Wait(ct);

            var samples = new List<double>(Samples);
            for (int i = 0; i < Samples; i++)
            {
                ct.ThrowIfCancellationRequested();

                if (i > 0) Task.Delay(SampleMs, ct).Wait(ct);

                float total = 0;
                foreach (var c in counters)
                {
                    try { total += c.NextValue(); } catch { }
                }
                samples.Add(Math.Clamp(total, 0.0, 100.0));
            }

            foreach (var c in counters) c.Dispose();

            var medianUsage = Median(samples);
            var score = (int)Math.Round(100 - medianUsage);
            return new PerformanceCategoryScore("GPU", score, $"{medianUsage:0}% en uso");
        }
        catch (Exception ex)
        {
            return new PerformanceCategoryScore("GPU", 50, $"No medible: {ex.Message}");
        }
    }

    // ── Red (multi-muestra, mediana) ─────────────────────────────────────────

    private static PerformanceCategoryScore MeasureNetworkMultiSample(CancellationToken ct)
    {
        var rtts = new List<long>(Samples);

        for (int i = 0; i < Samples; i++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using var ping = new Ping();
                var reply = ping.Send("1.1.1.1", timeout: 2000);
                if (reply.Status == IPStatus.Success)
                    rtts.Add(reply.RoundtripTime);
            }
            catch { /* sin red o error transitorio */ }
        }

        if (rtts.Count == 0)
            return new PerformanceCategoryScore("Red", 50, "Sin respuesta (1.1.1.1)");

        var medianRtt = (long)Median(rtts.Select(r => (double)r).ToList());
        int score;
        if (medianRtt <= 20)       score = 100;
        else if (medianRtt >= 300) score = 0;
        else score = (int)Math.Round(100 - (medianRtt - 20) / 280.0 * 100);

        return new PerformanceCategoryScore("Red", score, $"{medianRtt} ms hasta 1.1.1.1");
    }

    // ── RAM (snapshot — muy estable) ─────────────────────────────────────────

    private static PerformanceCategoryScore MeasureRam()
    {
        var status = new MEMORYSTATUSEX();
        if (!GlobalMemoryStatusEx(status))
            return new PerformanceCategoryScore("Memoria", 50, "No se pudo medir");

        var load    = (int)status.dwMemoryLoad;
        var totalGb = status.ullTotalPhys / 1024.0 / 1024.0 / 1024.0;
        var availGb = status.ullAvailPhys / 1024.0 / 1024.0 / 1024.0;
        var score   = Math.Clamp(100 - load, 0, 100);
        return new PerformanceCategoryScore(
            "Memoria", score,
            $"{load}% en uso ({availGb:0.0} GB libres de {totalGb:0.0} GB)");
    }

    // ── Disco (snapshot — muy estable) ───────────────────────────────────────

    private static PerformanceCategoryScore MeasureDisk()
    {
        try
        {
            var sys   = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
            var drive = new DriveInfo(sys);
            if (!drive.IsReady)
                return new PerformanceCategoryScore("Disco", 50, "Disco no listo");

            var totalGb = drive.TotalSize            / 1024.0 / 1024.0 / 1024.0;
            var freeGb  = drive.AvailableFreeSpace   / 1024.0 / 1024.0 / 1024.0;
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

    // ── Procesos (snapshot — moderadamente estable) ──────────────────────────

    private static PerformanceCategoryScore MeasureProcesses()
    {
        try
        {
            var count = Process.GetProcesses().Length;
            int score;
            if (count <= 80)       score = 100;
            else if (count >= 300) score = 0;
            else score = (int)Math.Round(100 - (count - 80) / 220.0 * 100);

            return new PerformanceCategoryScore("Procesos", score, $"{count} procesos activos");
        }
        catch (Exception ex)
        {
            return new PerformanceCategoryScore("Procesos", 50, $"Error: {ex.Message}");
        }
    }

    // ── Apps de inicio (snapshot — muy estable) ──────────────────────────────

    private static PerformanceCategoryScore MeasureStartup()
    {
        try
        {
            var count = CountStartupEntries();
            int score;
            if (count <= 8)       score = 100;
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
                using var key  = root.OpenSubKey(path);
                if (key is null) continue;
                total += key.GetValueNames().Length;
            }
            catch { /* permisos */ }
        }
        return total;
    }

    // ── Temp (snapshot — estable) ────────────────────────────────────────────

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
            if (mb <= 0)         score = 100;
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

    // ── Helpers matemáticos ──────────────────────────────────────────────────

    /// <summary>Devuelve la mediana de una lista de valores. La lista puede estar desordenada.</summary>
    private static double Median(List<double> values)
    {
        if (values.Count == 0) return 0;
        var sorted = values.OrderBy(v => v).ToList();
        int mid = sorted.Count / 2;
        return sorted.Count % 2 == 0
            ? (sorted[mid - 1] + sorted[mid]) / 2.0
            : sorted[mid];
    }

    // ── P/Invoke ─────────────────────────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME
    {
        public uint dwLowDateTime;
        public uint dwHighDateTime;
    }

    private static ulong ToUInt64(FILETIME ft) =>
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
