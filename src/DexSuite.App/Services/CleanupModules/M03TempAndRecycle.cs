using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using DexSuite.App.Models;

namespace DexSuite.App.Services.CleanupModules;

/// <summary>
/// M3 — Temporales, Recientes y Papelera (+ Windows.old).
/// Vacía la papelera vía P/Invoke SHEmptyRecycleBin (sin diálogos, sin sonido).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class M03TempAndRecycle : ModuleExecutorBase
{
    public override int ModuleId => 3;

    [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint SHEmptyRecycleBin(IntPtr hwnd, string? rootPath, uint flags);

    private const uint SHERB_NOCONFIRMATION = 0x00000001;
    private const uint SHERB_NOPROGRESSUI   = 0x00000002;
    private const uint SHERB_NOSOUND        = 0x00000004;

    public override async IAsyncEnumerable<ModuleProgress> ExecuteAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        yield return Header("Temporales, Recientes y Papelera");
        long totalBytes = 0;
        int  totalFiles = 0;

        var windir   = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appData  = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var profile  = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        // Borrado agresivo de carpetas Temp (la app ya corre como admin, no necesita takeown).
        yield return Step("Borrando carpetas Temp con permisos extendidos");
        var tempPaths = new[]
        {
            Path.GetTempPath(),
            Path.Combine(windir, "Temp"),
            Path.Combine(localApp, "Temp"),
            Path.Combine(profile, "AppData", "Local", "Temp"),
        };
        foreach (var p in tempPaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (ct.IsCancellationRequested) yield break;
            var (f, b) = PurgeDirectory(p, ct);
            totalFiles += f; totalBytes += b;
        }
        yield return Ok($"Temporales borradas ({totalFiles} archivos, {FormatBytes(totalBytes)})");

        if (ct.IsCancellationRequested) yield break;
        yield return Step("Historial de archivos recientes");
        var (rf, rb) = PurgeDirectory(Path.Combine(appData, "Microsoft", "Windows", "Recent"), ct);
        totalFiles += rf; totalBytes += rb;
        yield return Ok($"Historial reciente limpiado ({rf} archivos)");

        if (ct.IsCancellationRequested) yield break;
        yield return Step("Papelera de Reciclaje");
        string? recycleErr = null;
        try
        {
            SHEmptyRecycleBin(IntPtr.Zero, null,
                SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);
        }
        catch (Exception ex) { recycleErr = ex.Message; }
        yield return recycleErr is null
            ? Ok("Papelera vaciada")
            : Warn($"No se pudo vaciar la papelera: {recycleErr}");

        // Windows.old — instalación anterior; puede pesar varios GB.
        if (ct.IsCancellationRequested) yield break;
        yield return Step("Carpeta Windows.old (instalación anterior de Windows)");
        var windowsOld = "C:\\Windows.old";
        if (Directory.Exists(windowsOld))
        {
            var (wf, wb) = PurgeDirectory(windowsOld, ct);
            try { Directory.Delete(windowsOld, recursive: true); } catch { /* en uso */ }
            totalFiles += wf; totalBytes += wb;
            yield return Ok($"Windows.old eliminado ({wf} archivos, {FormatBytes(wb)})");
        }
        else
        {
            yield return Info("Windows.old no encontrado, nada que hacer");
        }

        yield return Done($"M3 completado — {totalFiles} archivos, {FormatBytes(totalBytes)} liberados");
        await Task.CompletedTask;
    }
}
