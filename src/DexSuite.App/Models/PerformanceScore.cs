namespace DexSuite.App.Models;

/// <summary>
/// Una categoria del test de rendimiento (CPU, RAM, Disco, ...).
/// </summary>
/// <param name="Name">Nombre corto que se ve en la card (CPU, RAM, ...).</param>
/// <param name="Score">Puntuacion 0-100. Mas alto = mejor.</param>
/// <param name="Detail">Texto descriptivo con el dato bruto medido (p.ej. "47% en uso").</param>
public sealed record PerformanceCategoryScore(
    string Name,
    int Score,
    string Detail);

/// <summary>
/// Snapshot del rendimiento del equipo. Total = promedio de las categorias.
/// </summary>
public sealed record PerformanceScore(
    int Total,
    IReadOnlyList<PerformanceCategoryScore> Categories,
    DateTime Timestamp)
{
    /// <summary>
    /// Clave i18n del veredicto. El consumidor (VM, XAML) la traduce con
    /// ILocalizationService.Get(VerdictKey) o el converter KeyToTranslationConverter.
    /// </summary>
    public string Verdict => Total switch
    {
        >= 85 => "Verdict.Excellent",
        >= 70 => "Verdict.Good",
        >= 55 => "Verdict.Acceptable",
        >= 40 => "Verdict.Improvable",
        _ => "Verdict.Critical",
    };
}
