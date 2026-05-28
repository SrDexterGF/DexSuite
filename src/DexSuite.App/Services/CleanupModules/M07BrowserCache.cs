using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using DexSuite.App.Models;

namespace DexSuite.App.Services.CleanupModules;

/// <summary>
/// M7 — Cache de navegadores.
/// Edge, Chrome, Firefox (todos los perfiles), Brave, Opera/Opera GX, Vivaldi.
/// Solo carpetas de cache — perfil, contraseñas y marcadores no se tocan.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class M07BrowserCache : ModuleExecutorBase
{
    public override int ModuleId => 7;

    public override async IAsyncEnumerable<ModuleProgress> ExecuteAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        yield return Header("Cache de Navegadores");
        long totalBytes = 0;
        int  totalFiles = 0;

        var localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appData  = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        yield return Step("Microsoft Edge");
        var (ef, eb) = PurgeChromiumLikeCache(Path.Combine(localApp, "Microsoft", "Edge", "User Data", "Default"), ct);
        totalFiles += ef; totalBytes += eb;
        yield return Ok($"Edge limpiado ({ef} archivos, {FormatBytes(eb)})");

        if (ct.IsCancellationRequested) yield break;
        yield return Step("Google Chrome");
        var (cf, cb) = PurgeChromiumLikeCache(Path.Combine(localApp, "Google", "Chrome", "User Data", "Default"), ct);
        totalFiles += cf; totalBytes += cb;
        yield return Ok($"Chrome limpiado ({cf} archivos, {FormatBytes(cb)})");

        if (ct.IsCancellationRequested) yield break;
        yield return Step("Mozilla Firefox");
        var ffProfiles = Path.Combine(localApp, "Mozilla", "Firefox", "Profiles");
        int ffFiles = 0; long ffBytes = 0;
        if (Directory.Exists(ffProfiles))
        {
            foreach (var prof in Directory.EnumerateDirectories(ffProfiles))
            {
                if (ct.IsCancellationRequested) yield break;
                var (a, b) = PurgeDirectory(Path.Combine(prof, "cache2"), ct);
                var (c, d) = PurgeDirectory(Path.Combine(prof, "startupCache"), ct);
                ffFiles += a + c; ffBytes += b + d;
            }
        }
        totalFiles += ffFiles; totalBytes += ffBytes;
        yield return Ok($"Firefox limpiado ({ffFiles} archivos, {FormatBytes(ffBytes)})");

        if (ct.IsCancellationRequested) yield break;
        yield return Step("Brave");
        var (bf, bb) = PurgeChromiumLikeCache(Path.Combine(localApp, "BraveSoftware", "Brave-Browser", "User Data", "Default"), ct);
        totalFiles += bf; totalBytes += bb;
        yield return Ok($"Brave limpiado ({bf} archivos, {FormatBytes(bb)})");

        if (ct.IsCancellationRequested) yield break;
        yield return Step("Opera y Opera GX");
        var (of_, ob) = PurgeDirectory(Path.Combine(appData, "Opera Software", "Opera Stable", "Cache", "Cache_Data"), ct);
        var (og, ogb) = PurgeDirectory(Path.Combine(appData, "Opera Software", "Opera GX Stable", "Cache", "Cache_Data"), ct);
        totalFiles += of_ + og; totalBytes += ob + ogb;
        yield return Ok($"Opera / Opera GX limpiados ({of_ + og} archivos)");

        if (ct.IsCancellationRequested) yield break;
        yield return Step("Vivaldi");
        var (vf, vb) = PurgeChromiumLikeCache(Path.Combine(localApp, "Vivaldi", "User Data", "Default"), ct);
        totalFiles += vf; totalBytes += vb;
        yield return Ok($"Vivaldi limpiado ({vf} archivos)");

        yield return Done($"M7 completado — {totalFiles} archivos, {FormatBytes(totalBytes)} liberados");
        await Task.CompletedTask;
    }

    /// <summary>
    /// Patrón común de cache Chromium: Cache\Cache_Data, Code Cache, GPUCache.
    /// </summary>
    private static (int Files, long Bytes) PurgeChromiumLikeCache(string profileDefault, CancellationToken ct)
    {
        if (!Directory.Exists(profileDefault)) return (0, 0);
        var t1 = PurgeDirectory(Path.Combine(profileDefault, "Cache", "Cache_Data"), ct);
        var t2 = PurgeDirectory(Path.Combine(profileDefault, "Code Cache"), ct);
        var t3 = PurgeDirectory(Path.Combine(profileDefault, "GPUCache"), ct);
        return (t1.Files + t2.Files + t3.Files, t1.Bytes + t2.Bytes + t3.Bytes);
    }
}
