using DexSuite.App.Models;

namespace DexSuite.App.Services;

/// <summary>
/// Persiste la puntuación "Antes" entre sesiones de la app.
/// Permite al usuario comprobar la mejora días o semanas después.
/// </summary>
public interface IPerformanceBaselineService
{
    /// <summary>Guarda el baseline actual en disco. Sobreescribe el anterior.</summary>
    Task SaveAsync(PerformanceScore score);

    /// <summary>Carga el baseline guardado. Devuelve null si no existe o está corrupto.</summary>
    Task<PerformanceScore?> LoadAsync();

    /// <summary>Elimina el archivo de baseline.</summary>
    Task ClearAsync();
}
