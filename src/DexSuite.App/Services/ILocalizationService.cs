using System.ComponentModel;

namespace DexSuite.App.Services;

/// <summary>
/// Servicio de internacionalización (i18n).
/// Permite obtener strings traducidas y cambiar el idioma en runtime.
/// Implementa <see cref="INotifyPropertyChanged"/> sobre el indexer para que
/// los bindings de la markup extension {loc:T} se refresquen automáticamente
/// al cambiar de idioma, sin necesidad de reiniciar la app.
/// </summary>
public interface ILocalizationService : INotifyPropertyChanged
{
    /// <summary>
    /// Código del idioma activo (p. ej. "es", "en", "zh").
    /// Asignar un código nuevo cambia el idioma de la UI al instante.
    /// </summary>
    string CurrentLanguage { get; set; }

    /// <summary>Lista de idiomas soportados, con código y nombre nativo.</summary>
    IReadOnlyList<LanguageOption> AvailableLanguages { get; }

    /// <summary>Devuelve la cadena traducida para la clave dada.</summary>
    string Get(string key);

    /// <summary>
    /// Indexer para que la markup extension {loc:T Key=...} pueda hacer
    /// binding directo. Equivalente a Get(key).
    /// </summary>
    string this[string key] { get; }

    /// <summary>Se dispara cada vez que cambia el idioma activo.</summary>
    event EventHandler? LanguageChanged;
}

/// <summary>
/// Representa un idioma disponible en la app.
/// </summary>
/// <param name="Code">Código ISO (p. ej. "es", "zh").</param>
/// <param name="NativeName">Nombre en el propio idioma (p. ej. "Español", "中文").</param>
/// <param name="EnglishName">Nombre en inglés, para logs y debugging.</param>
/// <param name="Flag">Emoji de la bandera del país/región (p. ej. "🇪🇸").</param>
public sealed record LanguageOption(string Code, string NativeName, string EnglishName, string Flag)
{
    /// <summary>Texto que se muestra en el ComboBox: bandera + nombre nativo.</summary>
    public string DisplayName => $"{Flag}  {NativeName}";
}
