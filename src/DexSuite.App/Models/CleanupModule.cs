namespace DexSuite.App.Models;

/// <summary>
/// Metadatos de un módulo del .bat DexSuite_CleanUp.
/// Mientras el .bat siga siendo la fuente de verdad, <see cref="Id"/> coincide
/// con el número del menú manual del .bat (1..19 para módulos normales, 20 para juegos).
/// Cuando migremos cada módulo a C# nativo, este record no cambia; solo cambia el ejecutor.
///
/// Los campos *Key son claves i18n; el ModuleItemViewModel las traduce vía
/// ILocalizationService al idioma activo.
/// </summary>
/// <param name="Id">Número del menú del .bat.</param>
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
    string SafetyReasonKey);
