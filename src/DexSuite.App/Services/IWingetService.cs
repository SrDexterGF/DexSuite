namespace DexSuite.App.Services;

/// <summary>Resultado de una actualización masiva con winget.</summary>
public record WingetUpgradeResult(int PackagesUpdated, bool Succeeded);

/// <summary>
/// Actualiza todas las aplicaciones instaladas usando winget upgrade --all.
/// La operación puede tardar varios minutos dependiendo de los paquetes pendientes.
/// </summary>
public interface IWingetService
{
    /// <summary>True si winget.exe está disponible en el sistema.</summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Ejecuta winget upgrade --all y reporta cada línea de salida
    /// a través de <paramref name="progress"/>.
    /// </summary>
    Task<WingetUpgradeResult> UpgradeAllAsync(
        IProgress<string>? progress = null,
        CancellationToken ct = default);
}
