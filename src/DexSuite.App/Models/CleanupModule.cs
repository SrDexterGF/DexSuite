namespace DexSuite.App.Models;

/// <summary>
/// Metadatos de un módulo nativo de DexSuite (M01..M19, más M20 para juegos).
/// El Id coincide con el número de catálogo visible en la UI.
///
/// Los campos *Key son claves i18n; el ModuleItemViewModel las traduce vía
/// ILocalizationService al idioma activo.
/// </summary>
/// <param name="Id">Número del módulo en el catálogo (1..20).</param>
/// <param name="NameKey">Clave i18n del nombre corto. P. ej. "Modules.M01.Name".</param>
/// <param name="DescriptionKey">Clave i18n del resumen de una línea.</param>
/// <param name="Category">Familia funcional del módulo.</param>
/// <param name="Tier">Free / Avanzado / Pro.</param>
/// <param name="RecommendedDefault">Si arranca marcado al abrir la app.</param>
/// <param name="Reversible">true = se puede deshacer / no daña datos; false = cambios persistentes o destructivos.</param>
/// <param name="SafetyReasonKey">Clave i18n del texto que explica por qué es Seguro o Riesgo.</param>
public sealed record CleanupModule(
    int Id,
    string NameKey,
    string DescriptionKey,
    ModuleCategory Category,
    ModuleTier Tier,
    bool RecommendedDefault,
    bool Reversible,
    string SafetyReasonKey,
    ImpactLevel Impact = ImpactLevel.None);
