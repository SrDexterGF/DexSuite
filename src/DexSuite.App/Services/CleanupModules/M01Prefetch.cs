using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using DexSuite.App.Models;

namespace DexSuite.App.Services.CleanupModules;

/// <summary>
/// M1 — Prefetch, Cache y D3DSCache.
/// Limpia carpetas Prefetch, Windows\Temp, Temp del usuario,
/// Thumbnail / Icon Cache y D3DSCache (DirectX shader cache).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class M01Prefetch : ModuleExecutorBase
{
    public override int ModuleId => 1;

    public override async IAsyncEnumerable<ModuleProgress> ExecuteAsync(
        IReadOnlySet<string>? enabledSubOps,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        yield return Header("Prefetch, Cache y D3DSCache");
        long totalBytes = 0;
        int  totalFiles = 0;

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        if (Want(enabledSubOps, "M01_prefetch"))
        {
            yield return Step("Prefetch");
            var (f, b) = PurgeDirectory(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch"), ct);
            totalFiles += f; totalBytes += b;
            yield return Ok($"Prefetch limpiado ({f} archivos, {FormatBytes(b)})");
        }

        if (ct.IsCancellationRequested) yield break;
        if (Want(enabledSubOps, "M01_win_temp"))
        {
            yield return Step("Windows Temp");
            var (f, b) = PurgeDirectory(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"), ct);
            totalFiles += f; totalBytes += b;
            yield return Ok($"Windows Temp limpiado ({f} archivos, {FormatBytes(b)})");
        }

        // Temp del usuario — varias rutas pueden apuntar a sitios distintos.
        if (ct.IsCancellationRequested) yield break;
        if (Want(enabledSubOps, "M01_user_temp"))
        {
            yield return Step("Carpetas Temp del usuario");
            var userTemp     = Path.GetTempPath();
            var userProfile  = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            var paths = new[]
            {
                userTemp,
                Path.Combine(localAppData, "Temp"),
                Path.Combine(userProfile, "Local Settings", "Temp"),
            };

            long userBytes = 0;
            foreach (var p in paths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (ct.IsCancellationRequested) yield break;
                var (f, b) = PurgeDirectory(p, ct);
                totalFiles += f; totalBytes += b; userBytes += b;
            }
            yield return Ok($"Temporales del usuario limpiadas ({FormatBytes(userBytes)})");
        }

        if (ct.IsCancellationRequested) yield break;
        if (Want(enabledSubOps, "M01_thumb_icon"))
        {
            yield return Step("Thumbnail cache e Icon Cache");
            var explorerDir = Path.Combine(localAppData, "Microsoft", "Windows", "Explorer");
            var (tf, tb) = PurgePattern(explorerDir, "thumbcache_*.db");
            var (icf, icb) = PurgePattern(explorerDir, "iconcache_*.db");
            var iconDb = Path.Combine(localAppData, "IconCache.db");
            var iconDbBytes = PurgeFile(iconDb);
            totalFiles += tf + icf + (iconDbBytes > 0 ? 1 : 0);
            totalBytes += tb + icb + iconDbBytes;
            yield return Ok($"Thumbnail e Icon Cache borrados ({tf + icf} archivos)");
        }

        if (ct.IsCancellationRequested) yield break;
        if (Want(enabledSubOps, "M01_d3ds"))
        {
            yield return Step("DirectX Shader Cache (D3DSCache)");
            var (f, b) = PurgeDirectory(Path.Combine(localAppData, "D3DSCache"), ct);
            totalFiles += f; totalBytes += b;
            yield return Ok($"D3DSCache limpiado ({f} archivos, {FormatBytes(b)})");
        }

        yield return Done($"M1 completado — {totalFiles} archivos, {FormatBytes(totalBytes)} liberados");
        await Task.CompletedTask;
    }
}
