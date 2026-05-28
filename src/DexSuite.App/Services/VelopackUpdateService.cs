using Microsoft.Extensions.Logging;
using Velopack;
using Velopack.Sources;

namespace DexSuite.App.Services;

/// <summary>
/// Implementación real de <see cref="IUpdateService"/> basada en Velopack
/// con GitHub Releases como fuente de actualizaciones.
/// </summary>
public sealed class VelopackUpdateService : IUpdateService
{
    private const string RepoUrl = "https://github.com/SrDexterGF/DexSuite";

    private readonly UpdateManager _manager;
    private readonly ILogger<VelopackUpdateService> _logger;

    // Guardamos el UpdateInfo entre CheckForUpdates → DownloadAndApply.
    private UpdateInfo? _pendingUpdate;

    public bool IsInstalledBuild { get; }
    public string CurrentVersion { get; }

    public VelopackUpdateService(ILogger<VelopackUpdateService> logger)
    {
        _logger = logger;

        var source = new GithubSource(RepoUrl, null, false);
        _manager = new UpdateManager(source);

        IsInstalledBuild = _manager.IsInstalled;
        CurrentVersion = _manager.CurrentVersion?.ToString() ?? "0.1.0";

        _logger.LogInformation(
            "UpdateService iniciado. Instalado: {Installed} · Versión: {Version}",
            IsInstalledBuild, CurrentVersion);
    }

    public async Task<string?> CheckForUpdatesAsync(CancellationToken ct = default)
    {
        if (!IsInstalledBuild)
        {
            _logger.LogInformation("Entorno de desarrollo — omitiendo búsqueda de actualizaciones");
            return null;
        }

        try
        {
            _pendingUpdate = await _manager.CheckForUpdatesAsync().WaitAsync(ct);
            var newVer = _pendingUpdate?.TargetFullRelease?.Version?.ToString();
            _logger.LogInformation("Comprobación completada. Nueva versión: {Ver}", newVer ?? "ninguna");
            return newVer;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al buscar actualizaciones en GitHub");
            return null;
        }
    }

    public async Task DownloadAndApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        if (!IsInstalledBuild)
        {
            _logger.LogWarning("DownloadAndApply: no es build instalado, operación omitida");
            return;
        }
        if (_pendingUpdate is null)
        {
            _logger.LogWarning("DownloadAndApply: sin actualización pendiente, llama primero a CheckForUpdatesAsync");
            return;
        }

        try
        {
            _logger.LogInformation("Descargando actualización v{Ver}...",
                _pendingUpdate.TargetFullRelease?.Version);

            // Velopack 0.0.x espera Action<int>? como callback de progreso.
            Action<int>? progressAction = progress is not null
                ? p => progress.Report(p)
                : null;

            await _manager.DownloadUpdatesAsync(_pendingUpdate, progressAction).WaitAsync(ct);

            _logger.LogInformation("Descarga completada. Aplicando y reiniciando...");
            _manager.ApplyUpdatesAndRestart(_pendingUpdate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al aplicar la actualización");
            throw;
        }
    }
}
