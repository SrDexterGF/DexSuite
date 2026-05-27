using DexSuite.App.Models;

namespace DexSuite.App.Services;

/// <summary>
/// Servicio que conoce el catálogo de juegos optimizables y lanza el script
/// PowerShell correspondiente desde el repositorio público
/// <c>SrDexterGF/Game_Configs</c>.
///
/// Mantenemos el catálogo en código (no en JSON remoto) para que la app
/// pueda renderizar la ventana de selección sin red. Cuando se añade un
/// juego en GitHub, hay que añadirlo aquí — es manualmente seguro porque el
/// repo cambia con poca frecuencia.
/// </summary>
public interface IGameOptimizationService
{
    /// <summary>Catálogo de juegos soportados, listo para enlazar a la UI.</summary>
    IReadOnlyList<GameProfile> AvailableGames { get; }

    /// <summary>
    /// Lanza el script .ps1 de la variante indicada en una consola PowerShell
    /// elevada. El script se descarga vía <c>Invoke-WebRequest</c> y se
    /// ejecuta con <c>Invoke-Expression</c>; no se persiste en disco.
    /// </summary>
    /// <param name="variant">Variante del juego a optimizar.</param>
    Task RunGameOptimizationAsync(GameVariant variant, CancellationToken ct = default);
}
