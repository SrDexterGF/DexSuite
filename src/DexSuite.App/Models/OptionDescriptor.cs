namespace DexSuite.App.Models;

/// <summary>
/// Descriptor universal de cualquier opción mostrable en la UI (módulo,
/// ajuste, acción). Toda la presentación pasa por aquí, vía claves de i18n.
///
/// Esto permite añadir opciones nuevas sin tocar XAML: basta con crear
/// el descriptor + añadir las claves a Strings.resx, y la UI lo renderiza
/// con su nombre, descripción, tooltip y badge de impacto automáticamente.
/// </summary>
/// <param name="Id">Identificador estable (no traducible). P. ej. "M01" o "settings.lang".</param>
/// <param name="NameKey">Clave i18n para el nombre corto mostrado.</param>
/// <param name="DescriptionKey">Clave i18n para la descripción corta (debajo del nombre).</param>
/// <param name="HelpKey">Clave i18n para el texto del botón "?" (ayuda extendida).</param>
/// <param name="Impact">Nivel de impacto en rendimiento (badge visual).</param>
/// <param name="Tier">Plan en el que está disponible (Free/Avanzado/Pro).</param>
/// <param name="RecommendedDefault">Si arranca marcado al abrir la app.</param>
/// <param name="Reversible">Si el cambio se puede deshacer sin restaurar Windows.</param>
public sealed record OptionDescriptor(
    string Id,
    string NameKey,
    string DescriptionKey,
    string HelpKey,
    ImpactLevel Impact,
    ModuleTier Tier,
    bool RecommendedDefault,
    bool Reversible);
