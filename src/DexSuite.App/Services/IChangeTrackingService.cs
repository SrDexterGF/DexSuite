using DexSuite.App.Models;

namespace DexSuite.App.Services;

/// <summary>
/// Resultado agregado de una operación de reversión.
/// </summary>
public record RevertResult(int Total, int Reverted, int Failed);

/// <summary>
/// Servicio de auditoría y reversión de cambios aplicados por módulos nativos.
///
/// USO POR LOS MÓDULOS C# (futuros):
///   1. Antes de aplicar el cambio, llamar a <see cref="RecordRegistryChangeAsync"/>,
///      <see cref="RecordServiceChangeAsync"/>, etc., con el VALOR ORIGINAL.
///   2. Aplicar el cambio normalmente.
///   3. Si falla, llamar a <see cref="RevertChangeAsync"/> con el Id devuelto.
///
/// USO POR LA UI (vista "Revertir cambios"):
///   - Listar cambios pendientes con <see cref="GetPendingChangesAsync"/>.
///   - Revertir individualmente o por módulo con <see cref="RevertModuleChangesAsync"/>.
/// </summary>
public interface IChangeTrackingService
{
    /// <summary>Registra un cambio en el registro de Windows.</summary>
    Task<int> RecordRegistryChangeAsync(
        string moduleId, string moduleName,
        string keyPath, string? valueName,
        string? originalValue, string? newValue,
        string? valueKind);

    /// <summary>Registra un cambio en el tipo de inicio de un servicio.</summary>
    Task<int> RecordServiceChangeAsync(
        string moduleId, string moduleName,
        string serviceName,
        string? originalStartType, string? newStartType);

    /// <summary>Registra un cambio en una tarea programada (Enabled/Disabled).</summary>
    Task<int> RecordScheduledTaskChangeAsync(
        string moduleId, string moduleName,
        string taskPath,
        string? originalEnabled, string? newEnabled);

    /// <summary>
    /// Registra un cambio de registro SOLO si aún no existe un registro pendiente
    /// para (moduleId, keyPath, valueName). Garantiza que el valor original
    /// capturado es el de la PRIMERA aplicación, no el ya modificado por DexSuite
    /// en ejecuciones posteriores. Idempotente.
    /// </summary>
    Task RecordRegistryChangeIfFirstAsync(
        string moduleId, string moduleName,
        string keyPath, string? valueName,
        string? originalValue, string? newValue,
        string? valueKind);

    /// <summary>
    /// Como <see cref="RecordRegistryChangeIfFirstAsync"/> pero para el tipo de
    /// inicio de un servicio. Captura el StartMode original una sola vez.
    /// </summary>
    Task RecordServiceChangeIfFirstAsync(
        string moduleId, string moduleName,
        string serviceName,
        string? originalStartType, string? newStartType);

    /// <summary>Devuelve todos los cambios no revertidos (más recientes primero).</summary>
    Task<IReadOnlyList<ModuleChangeRecord>> GetPendingChangesAsync();

    /// <summary>Devuelve TODOS los cambios (revertidos o no), para auditoría.</summary>
    Task<IReadOnlyList<ModuleChangeRecord>> GetAllChangesAsync(int max = 1000);

    /// <summary>Revierte un cambio concreto por Id. Marca el registro como revertido.</summary>
    Task<bool> RevertChangeAsync(int changeId, CancellationToken ct = default);

    /// <summary>Revierte TODOS los cambios pendientes de un módulo.</summary>
    Task<RevertResult> RevertModuleChangesAsync(string moduleId, CancellationToken ct = default);

    /// <summary>Revierte TODOS los cambios pendientes en orden inverso (LIFO).</summary>
    Task<RevertResult> RevertAllPendingAsync(CancellationToken ct = default);

    /// <summary>Cuántos cambios pendientes hay sin cargar la lista entera.</summary>
    Task<int> CountPendingAsync();
}
