namespace DexSuite.App.Services;

/// <summary>
/// Persiste el estado "aplicado" de cada módulo entre sesiones.
/// La UI lo consulta al arrancar para pintar el indicador correcto
/// (barra = no aplicado, tick = aplicado) y lo actualiza al terminar un run.
/// </summary>
public interface IModuleStateService
{
    /// <summary>Devuelve los Ids de módulo que están marcados como aplicados.</summary>
    Task<IReadOnlyCollection<int>> GetAppliedModuleIdsAsync(CancellationToken ct = default);

    /// <summary>Marca (o desmarca) un módulo como aplicado y lo persiste.</summary>
    Task SetAppliedAsync(int moduleId, bool applied, CancellationToken ct = default);
}
