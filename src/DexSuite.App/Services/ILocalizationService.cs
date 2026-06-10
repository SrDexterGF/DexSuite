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
/// <param name="Flag">Emoji de la bandera (reservado; Windows no lo renderiza en color).</param>
public sealed record LanguageOption(string Code, string NativeName, string EnglishName, string Flag)
{
    /// <summary>Nombre nativo del idioma.</summary>
    public string DisplayName => NativeName;

    /// <summary>Código ISO en mayúsculas para el chip del selector ("ES", "RU", "JA"…).</summary>
    public string UpperCode => Code.ToUpperInvariant();

    /// <summary>
    /// Código de país ISO 3166-1 alpha-2 para cargar la bandera desde flagcdn.com.
    /// Los idiomas regionales sin estado propio (gl, ca, eu) usan el código de España.
    /// </summary>
    public string FlagCountryCode => Code switch
    {
        "es" or "gl" or "ca" or "eu" => "es",
        "en" => "gb",
        "pt" => "pt",
        "fr" => "fr",
        "de" => "de",
        "it" => "it",
        "zh" => "cn",
        "ru" => "ru",
        "uk" => "ua",
        "ar" => "sa",
        "ja" => "jp",
        "ko" => "kr",
        "hi" => "in",
        "bn" => "bd",
        "ur" => "pk",
        "id" => "id",
        "tr" => "tr",
        "vi" => "vn",
        "nl" => "nl",
        "sv" => "se",
        "ro" => "ro",
        "pl" => "pl",
        "cs" => "cz",
        "el" => "gr",
        "da" => "dk",
        "no" => "no",
        "fi" => "fi",
        "bg" => "bg",
        "hu" => "hu",
        "pt-BR" => "br",
        "th" => "th",
        "zh-TW" => "tw",
        _    => Code,
    };

    /// <summary>
    /// URL de la bandera. Los idiomas regionales sin país propio usan
    /// Wikipedia Commons (banderas oficiales de Galicia, Cataluña y Euskadi).
    /// El resto carga desde flagcdn.com (20×15 px, PNG, sin API key).
    /// </summary>
    public string FlagImageUrl => Code switch
    {
        "gl" => "https://upload.wikimedia.org/wikipedia/commons/thumb/6/64/Flag_of_Galicia.svg/20px-Flag_of_Galicia.svg.png",
        "ca" => "https://upload.wikimedia.org/wikipedia/commons/thumb/c/ce/Flag_of_Catalonia.svg/20px-Flag_of_Catalonia.svg.png",
        "eu" => "https://upload.wikimedia.org/wikipedia/commons/thumb/2/2d/Flag_of_the_Basque_Country.svg/20px-Flag_of_the_Basque_Country.svg.png",
        _    => $"https://flagcdn.com/20x15/{FlagCountryCode}.png",
    };
}
