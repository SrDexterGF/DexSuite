using System.Runtime.CompilerServices;
using DexSuite.App.Models;
using DexSuite.App.Services.CleanupModules;
using Microsoft.Extensions.Logging;

namespace DexSuite.App.Services;

/// <summary>
/// Implementación de <see cref="INativeModuleRunner"/>. Recibe por DI todos los
/// <see cref="IModuleExecutor"/> registrados (uno por módulo M1..M19), los indexa
/// por ModuleId y los ejecuta en orden al recibir RunAsync.
///
/// Cada módulo emite su propio Header/Step/Ok/Done — el runner solo encadena.
/// </summary>
public sealed class NativeModuleRunner : INativeModuleRunner
{
    private readonly IReadOnlyDictionary<int, IModuleExecutor> _executors;
    private readonly ILogger<NativeModuleRunner> _logger;

    public NativeModuleRunner(IEnumerable<IModuleExecutor> executors, ILogger<NativeModuleRunner> logger)
    {
        _executors = executors.ToDictionary(e => e.ModuleId);
        _logger = logger;
    }

    public async IAsyncEnumerable<ModuleProgress> RunAsync(
        IReadOnlyList<int> selectedModuleIds,
        IReadOnlyDictionary<int, IReadOnlySet<string>>? subOptionsByModule = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var id in selectedModuleIds.OrderBy(i => i))
        {
            if (ct.IsCancellationRequested) yield break;

            if (!_executors.TryGetValue(id, out var executor))
            {
                _logger.LogWarning("No hay executor nativo para el módulo {Id}", id);
                yield return ModuleProgress.Warn(id, $"[!] Módulo {id} sin implementación nativa, omitido.");
                continue;
            }

            // null → vista simple (ejecuta todo). Set vacío o con ids → vista avanzada.
            IReadOnlySet<string>? subOps = null;
            subOptionsByModule?.TryGetValue(id, out subOps);

            ModuleProgress? lastEvent = null;
            await foreach (var p in executor.ExecuteAsync(subOps, ct).WithCancellation(ct))
            {
                lastEvent = p;
                yield return p;
            }

            // Si el módulo no emitió un Done explícito, lo emitimos nosotros para
            // que la UI cierre la fila del módulo correctamente.
            if (lastEvent is null || lastEvent.Kind != ModuleProgressKind.Done)
                yield return ModuleProgress.Done(id, "Completado");
        }
    }
}
