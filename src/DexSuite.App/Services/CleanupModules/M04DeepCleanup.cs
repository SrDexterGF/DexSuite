using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using DexSuite.App.Models;

namespace DexSuite.App.Services.CleanupModules;

/// <summary>
/// M4 — Limpieza profunda.
/// Crash dumps, cola de impresión (spooler), compactación de WMI y vaciado del Visor de Eventos.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class M04DeepCleanup : ModuleExecutorBase
{
    public override int ModuleId => 4;

    public override async IAsyncEnumerable<ModuleProgress> ExecuteAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        yield return Header("Limpieza Profunda");
        long totalBytes = 0;
        int  totalFiles = 0;

        var windir   = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        yield return Step("Crash dumps y volcados de memoria");
        totalBytes += PurgeFile("C:\\WIN386.SWP");
        var (mdf, mdb) = PurgeDirectory(Path.Combine(windir, "Minidump"), ct);
        totalBytes += PurgeFile(Path.Combine(windir, "memory.dmp"));
        var (cdf, cdb) = PurgeDirectory(Path.Combine(localApp, "CrashDumps"), ct);
        totalFiles += mdf + cdf;
        totalBytes += mdb + cdb;
        yield return Ok($"Crash dumps eliminados ({FormatBytes(totalBytes)})");

        if (ct.IsCancellationRequested) yield break;
        yield return Step("Cola de impresión (spooler)");
        StopService("Spooler");
        var (sf, sb) = PurgeDirectory(Path.Combine(system32, "spool", "PRINTERS"), ct);
        StartService("Spooler");
        totalFiles += sf; totalBytes += sb;
        yield return Ok("Spooler limpiado y reiniciado");

        if (ct.IsCancellationRequested) yield break;
        yield return Step("Compactando base de datos WMI");
        var winmgmt = Path.Combine(system32, "wbem", "WinMgmt.exe");
        if (File.Exists(winmgmt))
        {
            var rc = await RunProcessAsync(winmgmt, "/salvagerepository", ct);
            yield return rc == 0
                ? Ok("WMI compactado")
                : Warn($"winmgmt /salvagerepository devolvió ExitCode={rc}");
        }
        else
        {
            yield return Warn("winmgmt no encontrado en system32\\wbem");
        }

        if (ct.IsCancellationRequested) yield break;
        yield return Step("Vaciando el Visor de Eventos");
        int cleared = 0;
        int failed  = 0;
        string? enumErr = null;
        try
        {
            using var session = EventLogSession.GlobalSession;
            foreach (var logName in session.GetLogNames())
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    session.ClearLog(logName);
                    cleared++;
                }
                catch
                {
                    // Logs como "Security" pueden requerir privilegios SE_AUDIT_NAME.
                    // O ser canales analíticos que no se pueden vaciar online.
                    failed++;
                }
            }
        }
        catch (Exception ex) { enumErr = ex.Message; }
        yield return enumErr is null
            ? Ok($"{cleared} registros vaciados ({failed} no se pudieron procesar)")
            : Warn($"No se pudo enumerar el Visor de Eventos: {enumErr}");

        yield return Done($"M4 completado — {totalFiles} archivos, {FormatBytes(totalBytes)} liberados, {cleared} logs vaciados");
    }
}
