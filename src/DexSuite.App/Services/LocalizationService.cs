using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Threading;

namespace DexSuite.App.Services;

/// <summary>
/// Implementación principal de <see cref="ILocalizationService"/>.
/// Carga traducciones desde los .resx embebidos y expone un singleton
/// estático para la markup extension {loc:T}.
/// </summary>
public sealed class LocalizationService : ILocalizationService
{
    /// <summary>Singleton accesible desde la markup extension TExtension.</summary>
    public static LocalizationService Instance { get; } = new();

    private static readonly ResourceManager _rm = new(
        "DexSuite.App.Resources.Strings",
        typeof(LocalizationService).Assembly);

    private string _currentLanguage = "es";

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? LanguageChanged;

    /// <summary>
    /// Los 30 idiomas soportados por DexSuite.
    /// El nombre nativo se muestra en el ComboBox de Ajustes (cada usuario
    /// reconoce el suyo aunque la app esté en otro idioma).
    /// El chino "zh" se entrega como mandarín simplificado (zh-CN).
    /// </summary>
    public IReadOnlyList<LanguageOption> AvailableLanguages { get; } = new[]
    {
        // Español primero — idioma del desarrollador y principal de la app
        new LanguageOption("es", "Español",         "Spanish",             "🇪🇸"),
        // Europeos latino/germánico por nombre nativo
        new LanguageOption("ca", "Català",          "Catalan",             "🇪🇸"),
        new LanguageOption("cs", "Čeština",         "Czech",               "🇨🇿"),
        new LanguageOption("da", "Dansk",           "Danish",              "🇩🇰"),
        new LanguageOption("de", "Deutsch",         "German",              "🇩🇪"),
        new LanguageOption("en", "English",         "English",             "🇬🇧"),
        new LanguageOption("eu", "Euskara",         "Basque",              "🇪🇸"),
        new LanguageOption("fi", "Suomi",           "Finnish",             "🇫🇮"),
        new LanguageOption("fr", "Français",        "French",              "🇫🇷"),
        new LanguageOption("gl", "Galego",          "Galician",            "🇪🇸"),
        new LanguageOption("hu", "Magyar",          "Hungarian",           "🇭🇺"),
        new LanguageOption("id", "Indonesia",       "Indonesian",          "🇮🇩"),
        new LanguageOption("it", "Italiano",        "Italian",             "🇮🇹"),
        new LanguageOption("nl", "Nederlands",      "Dutch",               "🇳🇱"),
        new LanguageOption("no", "Norsk",           "Norwegian",           "🇳🇴"),
        new LanguageOption("pl", "Polski",          "Polish",              "🇵🇱"),
        new LanguageOption("pt", "Português",       "Portuguese",          "🇵🇹"),
        new LanguageOption("pt-BR", "Português (Brasil)", "Portuguese (Brazil)", "🇧🇷"),
        new LanguageOption("ro", "Română",          "Romanian",            "🇷🇴"),
        new LanguageOption("sv", "Svenska",         "Swedish",             "🇸🇪"),
        new LanguageOption("tr", "Türkçe",          "Turkish",             "🇹🇷"),
        new LanguageOption("vi", "Tiếng Việt",      "Vietnamese",          "🇻🇳"),
        // Griego y cirílico
        new LanguageOption("el", "Ελληνικά",        "Greek",               "🇬🇷"),
        new LanguageOption("ru", "Русский",         "Russian",             "🇷🇺"),
        new LanguageOption("uk", "Українська",      "Ukrainian",           "🇺🇦"),
        new LanguageOption("bg", "Български",       "Bulgarian",           "🇧🇬"),
        // RTL
        new LanguageOption("ur", "اردو",            "Urdu",                "🇵🇰"),
        new LanguageOption("ar", "العربية",         "Arabic",              "🇸🇦"),
        // Scripts asiáticos
        new LanguageOption("hi", "हिन्दी",            "Hindi",               "🇮🇳"),
        new LanguageOption("bn", "বাংলা",            "Bengali",             "🇧🇩"),
        new LanguageOption("zh", "中文 (简体)",      "Chinese (Mandarin)",  "🇨🇳"),
        new LanguageOption("zh-TW", "繁體中文",     "Chinese (Traditional)", "🇹🇼"),
        new LanguageOption("ja", "日本語",           "Japanese",            "🇯🇵"),
        new LanguageOption("ko", "한국어",           "Korean",              "🇰🇷"),
        new LanguageOption("th", "ไทย",             "Thai",                "🇹🇭"),
    };

    public string CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            if (string.Equals(_currentLanguage, value, StringComparison.OrdinalIgnoreCase))
                return;

            _currentLanguage = value;
            ApplyCulture(value);

            // Notifica al indexer "Item[]" para que todos los bindings
            // {loc:T Key=...} se refresquen automáticamente.
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentLanguage)));
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public string Get(string key)
    {
        if (string.IsNullOrEmpty(key)) return string.Empty;
        var culture = CultureFor(_currentLanguage);
        return _rm.GetString(key, culture) ?? $"[{key}]";
    }

    public string this[string key] => Get(key);

    private LocalizationService()
    {
        ApplyCulture(_currentLanguage);
    }

    private static void ApplyCulture(string code)
    {
        var culture = CultureFor(code);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
    }

    private static CultureInfo CultureFor(string code)
    {
        try { return CultureInfo.GetCultureInfo(code); }
        catch { return CultureInfo.InvariantCulture; }
    }
}
