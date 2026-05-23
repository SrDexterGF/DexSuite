using CommunityToolkit.Mvvm.ComponentModel;
using DexSuite.App.Models;

namespace DexSuite.App.ViewModels;

/// <summary>
/// Estado de ejecucion de un modulo dentro de un run.
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
/// Item de la lista de modulos en la UI. Envuelve un <see cref="CleanupModule"/>
/// y anade el estado mutable (marcado, estado de ejecucion).
/// </summary>
public partial class ModuleItemViewModel : ObservableObject
{
    public CleanupModule Module { get; }

    public ModuleItemViewModel(CleanupModule module, bool initiallyEnabled)
    {
        Module = module;
        isEnabled = initiallyEnabled;
    }

    public int Id => Module.Id;
    public string Name => Module.Name;
    public string Description => Module.Description;
    public string CategoryName => Module.Category.ToString();

    /// <summary>Etiqueta visible del tier (Free / Avanzado / Pro).</summary>
    public string TierLabel => Module.Tier switch
    {
        ModuleTier.Free => "Free",
        ModuleTier.Advanced => "Avanzado",
        ModuleTier.Pro => "Pro",
        _ => Module.Tier.ToString(),
    };

    /// <summary>Texto del Id en formato visual: "MODULO 03".</summary>
    public string IdLabel => $"MODULO {Id:00}";

    /// <summary>Texto de seguridad: reversible vs irreversible (sin localizar, uso interno).</summary>
    public string SafetyLabel => Module.Reversible ? "Seguro" : "Riesgo";

    /// <summary>Clave i18n del chip Seguro/Riesgo, para usar con KeyToTranslationConverter.</summary>
    public string SafetyLabelKey => Module.Reversible ? "Modules.Safety.Safe" : "Modules.Safety.Risk";

    /// <summary>Explicacion de por que es Seguro o Riesgo. Se muestra como tooltip del chip.</summary>
    public string SafetyReason => Module.SafetyReason;

    /// <summary>Si esta marcado para ejecutarse.</summary>
    [ObservableProperty]
    private bool isEnabled;

    /// <summary>Estado actual de ejecucion (para futura fase 2 con tracking por modulo).</summary>
    [ObservableProperty]
    private ModuleStatus status = ModuleStatus.Pending;
}
