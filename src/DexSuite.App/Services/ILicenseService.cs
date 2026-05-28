using DexSuite.App.Models;

namespace DexSuite.App.Services;

/// <summary>
/// Resultado de una operación de licencia (activación o re-verificación).
/// Se modela como record para que sea inmutable y fácil de loggear.
/// </summary>
public sealed record LicenseOperationResult(
    bool Success,
    ModuleTier Tier,
    string? Message);

/// <summary>
/// Servicio central de licencias. Encapsula:
///   • Activación: parsear clave, verificar firma RSA, validar HWID, persistir.
///   • Carga: leer la licencia almacenada y re-verificarla desde cero.
///   • Tier actual: <see cref="CurrentTier"/>, que la UI usa para desbloquear.
///   • Re-validación periódica (la dispara <see cref="LicenseWatchdog"/>).
///
/// Disparadores de cambio: <see cref="TierChanged"/> se emite cuando el tier
/// efectivo cambia (p.ej. activación, expiración, re-validación fallida).
/// </summary>
public interface ILicenseService
{
    /// <summary>Tier actualmente efectivo (Free si no hay licencia o falla la verificación).</summary>
    ModuleTier CurrentTier { get; }

    /// <summary>HWID del equipo (delegado en <see cref="IHardwareIdProvider"/>).</summary>
    string GetHardwareId();

    /// <summary>Aplica una clave de activación pegada por el usuario.</summary>
    Task<LicenseOperationResult> ActivateAsync(string activationKey, CancellationToken ct = default);

    /// <summary>
    /// Lee la licencia almacenada (si la hay) y la re-verifica desde cero.
    /// Llamar al arrancar y en cada tick del watchdog.
    /// </summary>
    Task<LicenseOperationResult> RevalidateAsync(CancellationToken ct = default);

    /// <summary>Elimina la licencia almacenada y vuelve a Free.</summary>
    Task DeactivateAsync(CancellationToken ct = default);

    /// <summary>Se dispara cuando <see cref="CurrentTier"/> cambia.</summary>
    event EventHandler<ModuleTier>? TierChanged;
}
