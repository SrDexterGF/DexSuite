using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DexSuite.App.Models;
using DexSuite.App.Services;
using Microsoft.Extensions.Logging;

namespace DexSuite.App.ViewModels;

/// <summary>
/// Tile observable de un juego dentro de la ventana de selección. Encapsula
/// el GameProfile + la variante seleccionada actualmente (relevante cuando el
/// juego tiene varias versiones, p. ej. Battlefield 1 / V / 2042).
/// </summary>
public partial class GameTileViewModel : ObservableObject
{
    public GameProfile Profile { get; }
    public string Name => Profile.Name;
    public string Subtitle => Profile.Subtitle;
    public IReadOnlyList<GameVariant> Variants => Profile.Variants;

    /// <summary>
    /// True solo si el juego tiene más de una variante (p. ej. Battlefield 1/3/4…).
    /// La UI usa este flag para ocultar el ComboBox cuando solo hay una
    /// optimización y evita mostrar al usuario un desplegable con un único
    /// ítem que repite el nombre del juego.
    /// </summary>
    public bool HasMultipleVariants => Profile.Variants.Count > 1;

    [ObservableProperty]
    private GameVariant selectedVariant;

    public GameTileViewModel(GameProfile profile)
    {
        Profile = profile;
        // Primera variante por defecto (siempre hay al menos una).
        selectedVariant = profile.Variants[0];
    }
}

/// <summary>
/// ViewModel de la ventana de selección de videojuegos. Expone la colección
/// de tiles y el comando que lanza la optimización de la variante elegida.
/// </summary>
public sealed partial class GameSelectorViewModel : ObservableObject
{
    private readonly IGameOptimizationService _service;
    private readonly IAppLogService _appLog;
    private readonly ILocalizationService _loc;
    private readonly ILogger<GameSelectorViewModel> _logger;

    public ObservableCollection<GameTileViewModel> Games { get; } = new();

    public GameSelectorViewModel(
        IGameOptimizationService service,
        IAppLogService appLog,
        ILocalizationService loc,
        ILogger<GameSelectorViewModel> logger)
    {
        _service = service;
        _appLog = appLog;
        _loc = loc;
        _logger = logger;

        foreach (var p in _service.AvailableGames)
            Games.Add(new GameTileViewModel(p));
    }

    /// <summary>
    /// Lanza la optimización del juego seleccionado en el tile. El método
    /// resuelve la variante actual y delega en el servicio. Cualquier fallo
    /// se loguea pero no rompe la UI (la ventana permanece abierta).
    /// </summary>
    [RelayCommand]
    private async Task OptimizeAsync(GameTileViewModel? tile)
    {
        if (tile is null) return;
        var variant = tile.SelectedVariant ?? tile.Variants[0];
        try
        {
            await _service.RunGameOptimizationAsync(variant);
            await _appLog.SuccessAsync(AppLogCategory.Run,
                string.Format(_loc.Get("Gaming.Log.Launched"), variant.DisplayName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo lanzando optimización de {Game}", variant.DisplayName);
            await _appLog.ErrorAsync(AppLogCategory.Run,
                string.Format(_loc.Get("Gaming.Log.Failed"), variant.DisplayName, ex.Message));
        }
    }
}
