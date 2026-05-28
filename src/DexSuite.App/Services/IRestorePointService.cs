namespace DexSuite.App.Services;

/// <summary>
/// Resultado de intentar crear un punto de restauración del sistema.
/// </summary>
public sealed record RestorePointResult(
    bool Success,
    string Message);

/// <summary>
/// Crea puntos de restauración de Windows mediante WMI (SystemRestore).
/// Requiere privilegios de administrador (ya garantizados por el manifiesto UAC).
/// </summary>
public interface IRestorePointService
{
    /// <summary>
    /// Crea un punto de restauración con la descripción indicada.
    /// Devuelve el resultado (ok o error) sin lanzar excepciones.
    /// </summary>
    Task<RestorePointResult> CreateAsync(string description, CancellationToken ct = default);

    /// <summary>
    /// Comprueba si la Restauración del sistema está habilitada en la unidad C:.
    /// Si está desactivada, <see cref="CreateAsync"/> siempre fallará.
    /// </summary>
    Task<bool> IsEnabledAsync();
}
