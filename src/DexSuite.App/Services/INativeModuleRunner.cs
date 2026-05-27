using DexSuite.App.Models;

namespace DexSuite.App.Services;

/// <summary>
/// Reemplazo nativo de <c>IBatRunner</c>. En vez de delegar en un .bat,
/// orquesta los <see cref="CleanupModules.IModuleExecutor"/> registrados en DI
/// y emite eventos estructurados (<see cref="ModuleProgress"/>) — sin parsear
/// stdout con regex.
/// </summary>
public interface INativeModuleRunner
{
    /// <summary>
    /// Ejecuta los módulos seleccionados en orden ascendente de id.
    /// Si un id no tiene executor registrado, se omite con un Warn.
    /// </summary>
    IAsyncEnumerable<ModuleProgress> RunAsync(
        IReadOnlyList<int> selectedModuleIds,
        CancellationToken ct = default);
}
