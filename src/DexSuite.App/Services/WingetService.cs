using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Logging;

namespace DexSuite.App.Services;

/// <summary>
/// Implementación de <see cref="IWingetService"/> que llama a winget.exe
/// de forma nativa (sin .bat ni PowerShell intermedio).
///
/// winget.exe está disponible desde Windows 10 1709+ cuando el paquete
/// "App Installer" está instalado (viene de serie en Windows 11 y en W10
/// actualizado desde la Store). Si no está disponible, <see cref="IsAvailable"/>
/// devuelve false y el comando en la UI queda deshabilitado.
/// </summary>
public sealed class WingetService : IWingetService
{
    private readonly ILogger<WingetService> _logger;

    // winget puede estar en PATH o en la ruta de instalación de MSIX.
    private static readonly string[] WingetCandidates =
    [
        "winget.exe",
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            @"Microsoft\WindowsApps\winget.exe"),
    ];

    private readonly string? _wingetPath;

    public bool IsAvailable => _wingetPath is not null;

    public WingetService(ILogger<WingetService> logger)
    {
        _logger = logger;
        _wingetPath = ResolveWinget();
        _logger.LogInformation("WingetService: {Status}", IsAvailable ? $"disponible en {_wingetPath}" : "no encontrado");
    }

    public async Task<WingetUpgradeResult> UpgradeAllAsync(
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        if (!IsAvailable)
            throw new InvalidOperationException("winget.exe no está disponible en este sistema.");

        int packagesUpdated = 0;
        bool succeeded = false;

        var psi = new ProcessStartInfo
        {
            FileName               = _wingetPath,
            Arguments              = "upgrade --all --accept-source-agreements --accept-package-agreements",
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow         = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        // Leemos stdout línea a línea sin bloquear el hilo.
        var readTask = Task.Run(async () =>
        {
            string? line;
            while ((line = await process.StandardOutput.ReadLineAsync(ct)) is not null)
            {
                if (ct.IsCancellationRequested) break;
                progress?.Report(line);

                // winget imprime el nombre del paquete + versión en cada actualización.
                // La línea de éxito incluye la palabra "Successfully" o el equivalente.
                if (line.Contains("Successfully", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("correctamente", StringComparison.OrdinalIgnoreCase))
                    packagesUpdated++;
            }
        }, ct);

        await readTask.ConfigureAwait(false);

        if (!ct.IsCancellationRequested)
            await process.WaitForExitAsync(ct).ConfigureAwait(false);

        succeeded = process.ExitCode == 0;
        _logger.LogInformation(
            "winget upgrade --all finalizado: ExitCode={Code}, PaquetesActualizados={Count}",
            process.ExitCode, packagesUpdated);

        return new WingetUpgradeResult(packagesUpdated, succeeded);
    }

    private static string? ResolveWinget()
    {
        foreach (var candidate in WingetCandidates)
        {
            try
            {
                // Si es solo "winget.exe" usamos PATH; si es ruta absoluta comprobamos existencia.
                if (!Path.IsPathRooted(candidate))
                {
                    var found = FindInPath(candidate);
                    if (found is not null) return found;
                }
                else if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch { /* ignora entradas inválidas */ }
        }
        return null;
    }

    private static string? FindInPath(string exe)
    {
        var paths = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator)
            ?? [];
        foreach (var dir in paths)
        {
            var full = Path.Combine(dir, exe);
            if (File.Exists(full)) return full;
        }
        return null;
    }
}
