using CommunityToolkit.Mvvm.ComponentModel;
using DexSuite.App.Models;
using DexSuite.App.Services;

namespace DexSuite.App.ViewModels;

/// <summary>
/// Estado de ejecución de un módulo dentro de un run.
/// </summary>
public enum ModuleStatus
{
    Pending,
    Running,
    Completed,
    Skipped,
    Failed,
}

/// <summary>
/// Item de la lista de módulos en la UI. Envuelve un <see cref="CleanupModule"/>
/// y traduce los textos al idioma activo vía <see cref="ILocalizationService"/>.
/// Cuando el usuario cambia el idioma, las propiedades Name/Description/SafetyReason
/// notifican PropertyChanged y la UI se refresca al instante.
/// </summary>
public partial class ModuleItemViewModel : ObservableObject, IDisposable
{
    private readonly ILocalizationService _loc;
    private readonly EventHandler _languageChangedHandler;

    public CleanupModule Module { get; }

    public ModuleItemViewModel(CleanupModule module, bool initiallyEnabled, ILocalizationService loc)
    {
        Module = module;
        _loc = loc;
        isEnabled = initiallyEnabled;

        _languageChangedHandler = (_, _) =>
        {
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(Description));
            OnPropertyChanged(nameof(SafetyReason));
            OnPropertyChanged(nameof(SafetyLabel));
            OnPropertyChanged(nameof(TierLabel));
        };
        _loc.LanguageChanged += _languageChangedHandler;
    }

    public int Id => Module.Id;
    public string Name => _loc.Get(Module.NameKey);
    public string Description => _loc.Get(Module.DescriptionKey);
    public string CategoryName => Module.Category.ToString();

    /// <summary>Etiqueta visible del tier (Free / Avanzado / Pro / …) traducida.</summary>
    public string TierLabel => Module.Tier switch
    {
        ModuleTier.Free => _loc.Get("Modules.Tier.Free"),
        ModuleTier.Advanced => _loc.Get("Modules.Tier.Advanced"),
        ModuleTier.Pro => _loc.Get("Modules.Tier.Pro"),
        _ => Module.Tier.ToString(),
    };

    /// <summary>Texto del chip Seguro/Riesgo, traducido al idioma activo.</summary>
    public string SafetyLabel => Module.Reversible
        ? _loc.Get("Modules.Safety.Safe")
        : _loc.Get("Modules.Safety.Risk");

    /// <summary>Clave i18n del chip, para usar con el converter (compatibilidad).</summary>
    public string SafetyLabelKey => Module.Reversible ? "Modules.Safety.Safe" : "Modules.Safety.Risk";

    /// <summary>Explicación de por qué es Seguro o Riesgo, traducida.</summary>
    public string SafetyReason => _loc.Get(Module.SafetyReasonKey);

    /// <summary>Nivel de impacto en rendimiento del módulo.</summary>
    public ImpactLevel Impact => Module.Impact;

    /// <summary>Clave i18n del badge de impacto (Suave / Notable / Fuerte / Extremo).</summary>
    public string ImpactLabelKey => Module.Impact switch
    {
        ImpactLevel.Soft    => "Modules.Impact.Soft",
        ImpactLevel.Notable => "Modules.Impact.Notable",
        ImpactLevel.Strong  => "Modules.Impact.Strong",
        ImpactLevel.Extreme => "Modules.Impact.Extreme",
        _                   => "Modules.Impact.None",
    };

    /// <summary>Si está marcado para ejecutarse.</summary>
    [ObservableProperty]
    private bool isEnabled;

    /// <summary>True cuando el tier del módulo supera el tier activo del usuario. Lo fija MainViewModel.</summary>
    [ObservableProperty]
    private bool isLocked;

    /// <summary>Estado actual de ejecución (para futura fase 2 con tracking por módulo).</summary>
    [ObservableProperty]
    private ModuleStatus status = ModuleStatus.Pending;

    public void Dispose()
    {
        _loc.LanguageChanged -= _languageChangedHandler;
    }
}
