using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using DexSuite.App.Models;

namespace DexSuite.App.Services.CleanupModules;

/// <summary>
/// M5 — Cache de Windows Update.
/// Para los servicios de WU (wuauserv, bits, cryptsvc, UsoSvc), mata TiWorker y
/// TrustedInstaller, vacía SoftwareDistribution\Download y vuelve a arrancar.
/// También limpia Delivery Optimization.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class M05WindowsUpdate : ModuleExecutorBase
{
    public override int ModuleId => 5;

    public override async IAsyncEnumerable<ModuleProgress> ExecuteAsync(
        IReadOnlySet<string>? enabledSubOps,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        yield return Header("Cache de Windows Update");

        var windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        int  df = 0;
        long db = 0;
        long totalBytes = 0;
        int  totalFiles = 0;

        if (Want(enabledSubOps, "M05_stop_services"))
        {
            yield return Step("Deteniendo servicios de Windows Update");
            StopService("wuauserv");
            StopService("bits");
            StopService("cryptsvc");
            StopService("UsoSvc");
            int killed = 0;
            killed += KillProcess("TiWorker.exe");
            killed += KillProcess("TrustedInstaller.exe");
            yield return Ok($"Servicios detenidos (procesos terminados: {killed})");
        }

        if (ct.IsCancellationRequested) yield break;
        if (Want(enabledSubOps, "M05_cache_dl"))
        {
            yield return Step("Borrando la carpeta de descargas de WU");
            var dl = Path.Combine(windir, "SoftwareDistribution", "Download");
            (df, db) = PurgeDirectory(dl, ct);
            totalFiles += df; totalBytes += db;
            yield return Ok($"Carpeta de descargas vaciada ({df} archivos, {FormatBytes(db)})");
        }

        if (ct.IsCancellationRequested) yield break;
        if (Want(enabledSubOps, "M05_restart_services"))
        {
            yield return Step("Reiniciando servicios de Windows Update");
            StartService("cryptsvc");
            StartService("bits");
            StartService("wuauserv");
            yield return Ok("Servicios reiniciados");
        }

        if (ct.IsCancellationRequested) yield break;
        if (Want(enabledSubOps, "M05_delivery_opt"))
        {
            yield return Step("Delivery Optimization Cache");
            var doCache = Path.Combine(windir, "ServiceProfiles", "NetworkService", "AppData", "Local",
                "Microsoft", "Windows", "DeliveryOptimization", "Cache");
            var doLogs = Path.Combine(windir, "ServiceProfiles", "NetworkService", "AppData", "Local",
                "Microsoft", "Windows", "DeliveryOptimization", "Logs");
            var doSd = Path.Combine(windir, "SoftwareDistribution", "DeliveryOptimization");

            var t1 = PurgeDirectory(doCache, ct);
            var t2 = PurgeDirectory(doLogs, ct);
            var t3 = PurgeDirectory(doSd, ct);

            totalFiles += t1.Files + t2.Files + t3.Files;
            totalBytes += t1.Bytes + t2.Bytes + t3.Bytes;
            yield return Ok($"Delivery Optimization limpiado ({t1.Files + t2.Files + t3.Files} archivos)");
        }

        yield return Done($"M5 completado — {totalFiles} archivos, {FormatBytes(totalBytes)} liberados");
        await Task.CompletedTask;
    }
}
