using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using DexSuite.App.Models;

namespace DexSuite.App.Services.CleanupModules;

/// <summary>
/// M2 — Logs del sistema.
/// Borra logs en C:\Windows (raíz y System32), SoftwareDistribution, INF,
/// CBS/DISM/WindowsUpdate y Windows Error Reporting.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class M02SystemLogs : ModuleExecutorBase
{
    public override int ModuleId => 2;

    public override async IAsyncEnumerable<ModuleProgress> ExecuteAsync(
        IReadOnlySet<string>? enabledSubOps,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        yield return Header("Logs del sistema");
        long totalBytes = 0;
        int  totalFiles = 0;

        var windir      = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var system32    = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var localApp    = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        if (Want(enabledSubOps, "M02_logs_root"))
        {
            yield return Step("Logs en la raíz de Windows");
            var (f, b) = PurgePattern(windir, "*.log");
            totalFiles += f; totalBytes += b;
            yield return Ok($"Logs raíz de Windows borrados ({f} archivos)");
        }

        if (ct.IsCancellationRequested) yield break;
        if (Want(enabledSubOps, "M02_logs_sys32"))
        {
            yield return Step("Logs de System32");
            var (f, b) = PurgePattern(system32, "*.log");
            totalFiles += f; totalBytes += b;
            yield return Ok($"Logs de System32 borrados ({f} archivos)");
        }

        if (ct.IsCancellationRequested) yield break;
        if (Want(enabledSubOps, "M02_logs_softwdist"))
        {
            yield return Step("Logs de SoftwareDistribution y Windows Update");
            var sd      = Path.Combine(windir, "SoftwareDistribution");
            var sdLogs1 = PurgePattern(sd, "*.log");
            var sdDataLogs = PurgeDirectory(Path.Combine(sd, "DataStore", "Logs"), ct);
            var wuLogBytes = PurgeFile(Path.Combine(windir, "WindowsUpdate.log"));
            var reportingBytes = PurgeFile(Path.Combine(sd, "ReportingEvents.log"));
            totalFiles += sdLogs1.Files + sdDataLogs.Files + (wuLogBytes > 0 ? 1 : 0) + (reportingBytes > 0 ? 1 : 0);
            totalBytes += sdLogs1.Bytes + sdDataLogs.Bytes + wuLogBytes + reportingBytes;
            yield return Ok("Logs de SoftwareDistribution borrados");
        }

        if (ct.IsCancellationRequested) yield break;
        if (Want(enabledSubOps, "M02_logs_inf"))
        {
            yield return Step("Logs de instalación de drivers (INF)");
            var (f, b) = PurgePattern(Path.Combine(windir, "inf"), "*.log");
            totalFiles += f; totalBytes += b;
            yield return Ok($"Logs INF borrados ({f} archivos)");
        }

        if (ct.IsCancellationRequested) yield break;
        if (Want(enabledSubOps, "M02_logs_cbs"))
        {
            yield return Step("Logs de CBS, DISM y WindowsUpdate");
            var (cf, cb)   = PurgePattern(Path.Combine(windir, "Logs", "CBS"), "*.log");
            var (df, db)   = PurgePattern(Path.Combine(windir, "Logs", "DISM"), "*.log");
            var (wf, wb)   = PurgePattern(Path.Combine(windir, "Logs", "WindowsUpdate"), "*.log");
            var (mf, mb)   = PurgePattern(Path.Combine(windir, "Logs", "MoSetup"), "*.log");
            totalFiles += cf + df + wf + mf;
            totalBytes += cb + db + wb + mb;
            yield return Ok($"Logs CBS / DISM / WU borrados ({cf + df + wf + mf} archivos)");
        }

        if (ct.IsCancellationRequested) yield break;
        if (Want(enabledSubOps, "M02_wer"))
        {
            yield return Step("Windows Error Reporting (WER)");
            var (werLf, werLb) = PurgeDirectory(Path.Combine(localApp, "Microsoft", "Windows", "WER"), ct);
            var (werGf, werGb) = PurgeDirectory(Path.Combine(programData, "Microsoft", "Windows", "WER"), ct);
            totalFiles += werLf + werGf;
            totalBytes += werLb + werGb;
            yield return Ok($"Carpeta WER vaciada ({werLf + werGf} archivos)");
        }

        yield return Done($"M2 completado — {totalFiles} archivos, {FormatBytes(totalBytes)} liberados");
        await Task.CompletedTask;
    }
}
