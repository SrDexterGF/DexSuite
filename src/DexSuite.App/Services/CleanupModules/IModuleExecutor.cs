using DexSuite.App.Models;

namespace DexSuite.App.Services.CleanupModules;

/// <summary>
/// Contrato común para cada uno de los módulos nativos del catálogo (M01..M19).
/// Cada implementación ejecuta su lógica en C# nativo
/// (System.IO / Registry / WMI / Process / P/Invoke).
///
/// La ejecución es un stream perezoso de <see cref="ModuleProgress"/>:
///   - Header   : la UI marca el módulo como Running.
///   - Step/Ok  : se renderizan línea a línea en el Log interno.
///   - Warn/Err : la UI los resalta y se persisten en SQLite.
///   - Done     : cierre del módulo (Completed o Error según haya habido fallos).
/// </summary>
public interface IModuleExecutor
{
    /// <summary>Id del módulo en el catálogo (1..19). Único.</summary>
    int ModuleId { get; }

    /// <summary>
    /// Lanza el módulo. Tiene que ser idempotente y respetar cancelación.
    /// </summary>
    /// <param name="enabledSubOps">
    /// Conjunto de Ids de sub-operaciones a ejecutar (vista avanzada). Si es
    /// null, el módulo ejecuta TODAS sus sub-operaciones (vista simple).
    /// </param>
    IAsyncEnumerable<ModuleProgress> ExecuteAsync(
        IReadOnlySet<string>? enabledSubOps,
        CancellationToken ct = default);
}
