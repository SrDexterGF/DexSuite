using CommunityToolkit.Mvvm.ComponentModel;
using DexSuite.App.Models;
using DexSuite.App.Services;

namespace DexSuite.App.ViewModels;

/// <summary>
/// Item de una sub-opción individual dentro de un módulo, para la vista avanzada.
/// Cada una se muestra como un checkbox con su nombre y descripción traducidos.
/// </summary>
public partial class ModuleSubOptionViewModel : ObservableObject, IDisposable
{
    private readonly ILocalizationService _loc;
    private readonly EventHandler _languageChangedHandler;

    public ModuleSubOption Model { get; }

    public ModuleSubOptionViewModel(ModuleSubOption model, ILocalizationService loc)
    {
        Model = model;
        _loc = loc;
        isEnabled = true; // por defecto todas marcadas (equivale a la vista simple).

        _languageChangedHandler = (_, _) =>
        {
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(Description));
        };
        _loc.LanguageChanged += _languageChangedHandler;
    }

    public string Id => Model.Id;
    public string Name => _loc.Get(Model.NameKey);
    public string Description => _loc.Get(Model.DescriptionKey);

    /// <summary>Si esta sub-opción se ejecuta al lanzar el módulo en vista avanzada.</summary>
    [ObservableProperty]
    private bool isEnabled;

    public void Dispose() => _loc.LanguageChanged -= _languageChangedHandler;
}
