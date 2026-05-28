using DexSuite.App.Models;

namespace DexSuite.App.Services;

/// <summary>
/// Analiza el rendimiento actual del equipo y devuelve un PerformanceScore.
/// </summary>
public interface IPerformanceAnalyzer
{
    Task<PerformanceScore> AnalyzeAsync(CancellationToken ct = default);
}
