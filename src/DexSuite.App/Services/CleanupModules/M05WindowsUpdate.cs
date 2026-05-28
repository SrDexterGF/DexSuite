using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using DexSuite.App.Models;

namespace DexSuite.App.Services.CleanupModules;

/// <summary>
/// M5 — Cache de Windows Update.
/// Para los servicios de WU (wuauserv, bits, cryptsvc, UsoSvc), mata TiWorker y
/// TrustedInstaller, vacía SoftwareDistribution\Download y vuelve a arrancar.
/// También limpia Delivery Optimization. Migrado del bloque RUN_5 del .bat.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class M05WindowsUpdate : ModuleExecutorBase
{
    public override int ModuleId => 5;

    public override async IAsyncEnumerable<ModuleProgress> ExecuteAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        yield return Header("Cache de Windows Update");

        var windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        // Parar servicios y procesos relacionados con WU.
        yield return Step("Deteniendo servicios de Windows Update");
        StopService("wuauserv");
        StopService("bits");
        StopService("cryptsvc");
        StopService("UsoSvc");
        int killed = 0;
        killed += KillProcess("TiWorker.exe");
        killed += KillProcess("TrustedInstaller.exe");
        yield return Ok($"Servicios detenidos (procesos terminados: {killed})");

        // Vaciar carpeta de descargas.
        if (ct.IsCancellationRequested) yield break;
        yield return Step("Borrando la carpeta de descargas de WU");
        var dl = Path.Combine(windir, "SoftwareDistribution", "Download");
        var (df, db) = PurgeDirectory(dl, ct);
        yield return Ok($"Carpeta de descargas vaciada ({df} archivos, {FormatBytes(db)})");

        // Reiniciar servicios.
        if (ct.IsCancellationRequested) yield break;
        yield return Step("Reiniciando servicios de Windows Update");
        StartService("cryptsvc");
        StartService("bits");
        StartService("wuauserv");
        yield return Ok("Servicios reiniciados");

        // Delivery Optimization: caches y logs.
        if (ct.IsCancellationRequested) yield break;
        yield return Step("Delivery Optimization Cache");
        var doCache = Path.Combine(windir, "ServiceProfiles", "NetworkService", "AppData", "Local",
            "Microsoft", "Windows", "DeliveryOptimization", "Cache");
        var doLogs = Path.Combine(windir, "ServiceProfiles", "NetworkService", "AppData", "Local",
            "Microsoft", "Windows", "DeliveryOptimization", "Logs");
        var doSd = Path.Combine(windir, "SoftwareDistribution", "DeliveryOptimization");

        var t1 = PurgeDirectory(doCache, ct);
        var t2 = PurgeDirectory(doLogs, ct);
        var t3 = PurgeDirectory(doSd, ct);

        var totalFiles = t1.Files + t2.Files + t3.Files;
        var totalBytes = t1.Bytes + t2.Bytes + t3.Bytes + db;
        yield return Ok($"Delivery Optimization limpiado ({t1.Files + t2.Files + t3.Files} archivos)");

        yield return Done($"M5 completado — {totalFiles + df} archivos, {FormatBytes(totalBytes)} liberados");
        await Task.CompletedTask;
    }
}
