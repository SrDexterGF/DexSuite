namespace DexSuite.App.Models;

/// <summary>
/// Metadatos de un modulo del .bat DexSuite_CleanUp.
/// Mientras el .bat sigue siendo la fuente de verdad, <see cref="Id"/> coincide
/// con el numero del menu manual del .bat (1..19 para modulos normales, 20 para juegos).
/// Cuando migremos cada modulo a C# nativo, este record no cambia; solo cambia el ejecutor.
/// </summary>
/// <param name="Id">Numero del menu del .bat.</param>
/// <param name="Name">Nombre corto para la card.</param>
/// <param name="Description">Resumen de una linea que se muestra debajo del nombre.</param>
/// <param name="Category">Familia funcional del modulo.</param>
/// <param name="Tier">Free / Avanzado / Pro.</param>
/// <param name="RecommendedDefault">Si arranca marcado al abrir la app.</param>
/// <param name="Reversible">true = se puede deshacer / no daña datos; false = cambios persistentes o destructivos.</param>
/// <param name="SafetyReason">Texto que explica por que es Seguro o Riesgo (se muestra como tooltip del chip).</param>
public sealed record CleanupModule(
    int Id,
    string Name,
    string Description,
    ModuleCategory Category,
    ModuleTier Tier,
    bool RecommendedDefault,
    bool Reversible,
    string SafetyReason);
