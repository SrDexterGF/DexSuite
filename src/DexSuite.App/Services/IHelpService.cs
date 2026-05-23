namespace DexSuite.App.Services;

/// <summary>
/// Servicio para mostrar la ayuda extendida (botón "?") de una opción.
/// Se traduce automáticamente al idioma activo vía <see cref="ILocalizationService"/>.
/// </summary>
public interface IHelpService
{
    /// <summary>
    /// Muestra una ventana de ayuda con el título y descripción dados
    /// (ambos son claves i18n, se traducen al idioma activo antes de mostrar).
    /// </summary>
    Task ShowHelpAsync(string titleKey, string descriptionKey);
}
