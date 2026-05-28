using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace DexSuite.App.Services;

/// <summary>
/// Limpieza rápida del sistema: vacía %TEMP%, Windows\Temp, Windows\Prefetch
/// y la papelera de reciclaje. Silencia errores de archivos bloqueados.
/// La app corre con UAC admin, por lo que tiene acceso a las carpetas de sistema.
/// </summary>
public sealed class QuickCleanService : IQuickCleanService
{
    private readonly ILogger<QuickCleanService> _logger;

    // P/Invoke para vaciar la papelera sin diálogos ni sonido.
    [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint SHEmptyRecycleBin(IntPtr hwnd, string? rootPath, uint flags);

    private const uint SHERB_NOCONFIRMATION = 0x00000001;
    private const uint SHERB_NOPROGRESSUI   = 0x00000002;
    private const uint SHERB_NOSOUND        = 0x00000004;

    public QuickCleanService(ILogger<QuickCleanService> logger)
    {
        _logger = logger;
    }

    public Task<QuickCleanResult> CleanAsync(CancellationToken ct = default)
        => Task.Run(() => DoClean(ct), ct);

    private QuickCleanResult DoClean(CancellationToken ct)
    {
        long totalBytes = 0;
        int  totalFiles = 0;

        // 1. %TEMP% — carpeta temporal del usuario
        var userTemp = Path.GetTempPath();
        var (f1, b1) = CleanDirectory(userTemp, ct);
        totalFiles += f1; totalBytes += b1;

        // 2. C:\Windows\Temp — temporales del sistema (requiere admin, ya lo somos)
        var winTemp = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp");
        var (f2, b2) = CleanDirectory(winTemp, ct);
        totalFiles += f2; totalBytes += b2;

        // 3. C:\Windows\Prefetch — puede requerir política especial; silenciamos si falla
        if (!ct.IsCancellationRequested)
        {
            var prefetch = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch");
            var (f3, b3) = CleanDirectory(prefetch, ct);
            totalFiles += f3; totalBytes += b3;
        }

        // 4. Papelera de reciclaje (todas las unidades)
        if (!ct.IsCancellationRequested)
        {
            try
            {
                SHEmptyRecycleBin(IntPtr.Zero, null,
                    SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo vaciar la papelera de reciclaje");
            }
        }

        _logger.LogInformation(
            "Limpieza rápida completada: {Files} archivos, {MB:F1} MB liberados",
            totalFiles, totalBytes / 1_048_576.0);

        return new QuickCleanResult(totalBytes, totalFiles);
    }

    /// <summary>Elimina todos los archivos y subdirectorios que pueda de un directorio.</summary>
    private static (int files, long bytes) CleanDirectory(string path, CancellationToken ct)
    {
        if (!Directory.Exists(path)) return (0, 0);

        int  files = 0;
        long bytes = 0;

        // Eliminar archivos (cualquier profundidad)
        try
        {
            foreach (var filePath in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    var fi = new FileInfo(filePath);
                    bytes += fi.Length;
                    fi.Delete();
                    files++;
                }
                catch { /* archivo en uso; se ignora */ }
            }
        }
        catch { /* enumeración fallida; continúa */ }

        // Eliminar subdirectorios vacíos o con restos
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(path))
            {
                if (ct.IsCancellationRequested) break;
                try { Directory.Delete(dir, recursive: true); }
                catch { /* directorio en uso; se ignora */ }
            }
        }
        catch { /* enumeración fallida; continúa */ }

        return (files, bytes);
    }
}
