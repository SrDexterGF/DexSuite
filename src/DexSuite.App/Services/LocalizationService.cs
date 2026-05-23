using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Threading;

namespace DexSuite.App.Services;

/// <summary>
/// Implementación principal de <see cref="ILocalizationService"/>.
///
/// Carga las traducciones desde los archivos .resx embebidos en
/// <c>Resources/Strings.resx</c> (idioma neutro = inglés) y
/// <c>Resources/Strings.&lt;lang&gt;.resx</c> (satellite assemblies).
///
/// Expone una instancia singleton <see cref="Instance"/> para que la
/// markup extension {loc:T} pueda enlazarse a ella sin pasar por DI.
/// Aun así también se registra en el contenedor DI para inyectarlo en
/// los ViewModels.
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
    /// Los 10 idiomas soportados por DexSuite.
    /// El nombre nativo se muestra en el ComboBox de Ajustes (cada usuario
    /// reconoce el suyo aunque la app esté en otro idioma).
    /// </summary>
    public IReadOnlyList<LanguageOption> AvailableLanguages { get; } = new[]
    {
        new LanguageOption("es", "Español",   "Spanish"),
        new LanguageOption("gl", "Galego",    "Galician"),
        new LanguageOption("ca", "Català",    "Catalan"),
        new LanguageOption("eu", "Euskara",   "Basque"),
        new LanguageOption("en", "English",   "English"),
        new LanguageOption("pt", "Português", "Portuguese"),
        new LanguageOption("fr", "Français",  "French"),
        new LanguageOption("de", "Deutsch",   "German"),
        new LanguageOption("it", "Italiano",  "Italian"),
        new LanguageOption("zh", "中文",       "Chinese"),
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
