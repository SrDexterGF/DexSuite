using System.IO;
using Microsoft.Extensions.Logging;

namespace DexSuite.App.Services;

/// <summary>
/// Implementación de <see cref="IAppSelfCleanupService"/>.
/// Busca carpetas con patrón "DexSuite_*" en %LocalAppData% —
/// son los directorios de staging que Velopack crea al aplicar una
/// actualización y que no siempre limpia solo.
///
/// Excluye explícitamente cualquier carpeta cuyo nombre empiece por
/// "DexSuiteKeyGen" (contiene la clave privada del desarrollador).
/// </summary>
public sealed class AppSelfCleanupService : IAppSelfCleanupService
{
    private static readonly string LocalAppData =
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    private readonly ILogger<AppSelfCleanupService> _logger;

    public AppSelfCleanupService(ILogger<AppSelfCleanupService> logger)
    {
        _logger = logger;
    }

    public Task<(int Folders, long Bytes)> CleanAsync(CancellationToken ct = default)
    {
        int  folders = 0;
        long bytes   = 0;

        string[] candidates;
        try
        {
            candidates = Directory.GetDirectories(LocalAppData, "DexSuite_*");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo enumerar %LocalAppData% al buscar staging folders");
            return Task.FromResult((0, 0L));
        }

        foreach (var dir in candidates)
        {
            ct.ThrowIfCancellationRequested();

            // Salvaguarda extra: nunca tocar DexSuiteKeyGen aunque cambiara el patrón.
            var name = Path.GetFileName(dir);
            if (name.StartsWith("DexSuiteKeyGen", StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                var di = new DirectoryInfo(dir);
                bytes += GetDirectorySize(di);
                di.Delete(recursive: true);
                folders++;
                _logger.LogInformation("Staging folder eliminado: {Dir}", dir);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo eliminar staging folder: {Dir}", dir);
            }
        }

        return Task.FromResult((folders, bytes));
    }

    private static long GetDirectorySize(DirectoryInfo di)
    {
        try
        {
            return di
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .Sum(f => { try { return f.Length; } catch { return 0L; } });
        }
        catch { return 0L; }
    }
}
