using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using DexSuite.App.Models;

namespace DexSuite.App.Services.CleanupModules;

/// <summary>
/// M9 — Store, OneDrive y Teams.
/// wsreset.exe -i resetea la cache de Microsoft Store sin abrir UI.
/// Borra logs de OneDrive y todas las cachés de Teams (clásico).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class M09StoreOneDriveTeams : ModuleExecutorBase
{
    public override int ModuleId => 9;

    public override async IAsyncEnumerable<ModuleProgress> ExecuteAsync(
        IReadOnlySet<string>? enabledSubOps,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        yield return Header("Store, OneDrive y Teams");
        long totalBytes = 0;
        int  totalFiles = 0;

        var localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appData  = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);

        if (Want(enabledSubOps, "M09_store"))
        {
            yield return Step("Reseteando la cache de Microsoft Store");
            var wsreset = Path.Combine(system32, "wsreset.exe");
            if (File.Exists(wsreset))
            {
                string? wsErr = null;
                bool started = false;
                Process? p = null;
                try
                {
                    p = Process.Start(new ProcessStartInfo
                    {
                        FileName        = wsreset,
                        Arguments       = "-i",
                        UseShellExecute = false,
                        CreateNoWindow  = true,
                        WindowStyle     = ProcessWindowStyle.Hidden,
                    });
                    started = p != null;
                }
                catch (Exception ex) { wsErr = ex.Message; }

                if (started && p != null)
                {
                    try { await p.WaitForExitAsync(ct).ConfigureAwait(false); }
                    catch (Exception ex) { wsErr = ex.Message; }
                    finally { p.Dispose(); }
                }

                yield return wsErr is not null
                    ? Warn($"wsreset falló: {wsErr}")
                    : started
                        ? Ok("Microsoft Store reseteada")
                        : Warn("wsreset no se pudo iniciar");
            }
            else yield return Warn("wsreset.exe no encontrado");
        }

        if (ct.IsCancellationRequested) yield break;
        if (Want(enabledSubOps, "M09_onedrive"))
        {
            yield return Step("Logs de OneDrive");
            var oneDrive = Path.Combine(localApp, "Microsoft", "OneDrive");
            if (Directory.Exists(oneDrive))
            {
                var (lf, lb) = PurgeDirectory(Path.Combine(oneDrive, "logs"), ct);
                var (sf, sb) = PurgeDirectory(Path.Combine(oneDrive, "setup", "logs"), ct);
                totalFiles += lf + sf; totalBytes += lb + sb;
                yield return Ok($"Logs de OneDrive borrados ({lf + sf} archivos)");
            }
            else yield return Info("OneDrive no instalado, omitido");
        }

        if (ct.IsCancellationRequested) yield break;
        if (Want(enabledSubOps, "M09_teams"))
        {
            yield return Step("Cache de Microsoft Teams");
            var teams = Path.Combine(appData, "Microsoft", "Teams");
            if (Directory.Exists(teams))
            {
                var subdirs = new[] { "Cache", "blob_storage", "databases", "GPUCache",
                                      "IndexedDB", "Local Storage", "tmp" };
                int tf = 0; long tb = 0;
                foreach (var sd in subdirs)
                {
                    if (ct.IsCancellationRequested) yield break;
                    var (f, b) = PurgeDirectory(Path.Combine(teams, sd), ct);
                    tf += f; tb += b;
                }
                totalFiles += tf; totalBytes += tb;
                yield return Ok($"Cache de Teams borrada ({tf} archivos, {FormatBytes(tb)})");
            }
            else yield return Info("Teams no instalado, omitido");
        }

        yield return Done($"M9 completado — {totalFiles} archivos, {FormatBytes(totalBytes)} liberados");
    }
}
