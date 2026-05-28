namespace DexSuite.App.Services;

/// <summary>
/// Abstracción sobre Velopack para buscar y aplicar actualizaciones.
/// Permite falsificar el servicio en tests y aislar la UI del SDK de Velopack.
/// </summary>
public interface IUpdateService
{
    /// <summary>Versión actualmente instalada (p. ej. "0.1.0").</summary>
    string CurrentVersion { get; }

    /// <summary>
    /// True si la app está corriendo dentro de un build instalado con Velopack.
    /// En entorno de desarrollo (bin/Debug) siempre es false.
    /// </summary>
    bool IsInstalledBuild { get; }

    /// <summary>
    /// Comprueba GitHub Releases en busca de una versión más nueva.
    /// Devuelve la versión disponible (p. ej. "0.2.0") o null si ya está al día
    /// o si no corre como build instalado.
    /// </summary>
    Task<string?> CheckForUpdatesAsync(CancellationToken ct = default);

    /// <summary>
    /// Descarga y aplica la actualización encontrada en la última llamada a
    /// <see cref="CheckForUpdatesAsync"/>. Reinicia la app automáticamente.
    /// No hace nada si no hay actualización pendiente o no es build instalado.
    /// </summary>
    Task DownloadAndApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default);
}
