using DexSuite.App.Data;
using DexSuite.App.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DexSuite.App.Services;

/// <summary>
/// Implementación de <see cref="IModuleStateService"/> sobre el SQLite de la app.
/// Una fila por módulo (clave = Id del catálogo); upsert idempotente.
/// </summary>
public sealed class ModuleStateService : IModuleStateService
{
    private readonly IDbContextFactory<DexSuiteDbContext> _factory;
    private readonly ILogger<ModuleStateService> _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public ModuleStateService(
        IDbContextFactory<DexSuiteDbContext> factory,
        ILogger<ModuleStateService> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<int>> GetAppliedModuleIdsAsync(CancellationToken ct = default)
    {
        try
        {
            await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
            return await db.ModuleStates
                .AsNoTracking()
                .Where(s => s.IsApplied)
                .Select(s => s.ModuleId)
                .ToListAsync(ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo leer el estado de los módulos");
            return Array.Empty<int>();
        }
    }

    public async Task SetAppliedAsync(int moduleId, bool applied, CancellationToken ct = default)
    {
        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
            var row = await db.ModuleStates.FindAsync([moduleId], ct).ConfigureAwait(false);
            if (row is null)
            {
                db.ModuleStates.Add(new ModuleStateRecord
                {
                    ModuleId     = moduleId,
                    IsApplied    = applied,
                    AppliedAtUtc = applied ? DateTime.UtcNow : null,
                });
            }
            else
            {
                row.IsApplied    = applied;
                row.AppliedAtUtc = applied ? DateTime.UtcNow : null;
            }
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo guardar el estado del módulo {ModuleId}", moduleId);
        }
        finally
        {
            _writeLock.Release();
        }
    }
}
