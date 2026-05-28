using System.Diagnostics;
using DexSuite.App.Models;
using Microsoft.Extensions.Logging;

namespace DexSuite.App.Services;

/// <summary>
/// Implementación de <see cref="IGameOptimizationService"/>. El catálogo está
/// sincronizado a mano con IWR.ps1 del repo Game_Configs.
/// </summary>
public sealed class GameOptimizationService : IGameOptimizationService
{
    private const string BaseRawUrl = "https://raw.githubusercontent.com/SrDexterGF/Game_Configs/main";

    private readonly ILogger<GameOptimizationService> _logger;

    public GameOptimizationService(ILogger<GameOptimizationService> logger)
    {
        _logger = logger;
    }

    // Cada variante es su propio perfil con una sola entrada → no hay ComboBox en la UI.
    public IReadOnlyList<GameProfile> AvailableGames { get; } = new[]
    {
        new GameProfile("PUBG Battlegrounds",        "Battle royale en tercera persona",
            new[] { new GameVariant("PUBG Battlegrounds", "PUBG BATTLEGROUNDS/PUBG BATTLEGROUNDS.ps1") }),
        new GameProfile("Frag Punk",                 "Shooter por habilidades",
            new[] { new GameVariant("Frag Punk", "Frag Punk/Frag Punk.ps1") }),
        new GameProfile("Splitgate 1",               "Arena shooter con portales (original)",
            new[] { new GameVariant("Splitgate 1", "Splitgate/Splitgate 1.ps1") }),
        new GameProfile("Splitgate 2",               "Arena shooter con portales (secuela)",
            new[] { new GameVariant("Splitgate 2", "Splitgate/Splitgate 2.ps1") }),
        new GameProfile("The Finals",                "FPS con destrucción de escenarios",
            new[] { new GameVariant("The Finals", "The Finals/The Finals.ps1") }),
        new GameProfile("ARC Raiders",               "Extraction shooter PvPvE",
            new[] { new GameVariant("ARC Raiders", "ARC Raiders/ARC Raiders.ps1") }),
        new GameProfile("Battlefield Bad Company 2", "Combates a gran escala (2010)",
            new[] { new GameVariant("Battlefield Bad Company 2", "Battlefield/Battlefield Bad Company 2.ps1") }),
        new GameProfile("Battlefield 3",             "Combates a gran escala (2011)",
            new[] { new GameVariant("Battlefield 3", "Battlefield/Battlefield 3.ps1") }),
        new GameProfile("Battlefield 4",             "Combates a gran escala (2013)",
            new[] { new GameVariant("Battlefield 4", "Battlefield/Battlefield 4.ps1") }),
        new GameProfile("Battlefield Hardline",      "Combates a gran escala (2015)",
            new[] { new GameVariant("Battlefield Hardline", "Battlefield/Battlefield Hardline.ps1") }),
        new GameProfile("Battlefield 1",             "Combates a gran escala (2016)",
            new[] { new GameVariant("Battlefield 1", "Battlefield/Battlefield 1.ps1") }),
        new GameProfile("Battlefield V",             "Combates a gran escala (2018)",
            new[] { new GameVariant("Battlefield V", "Battlefield/Battlefield V.ps1") }),
        new GameProfile("Battlefield 2042",          "Combates a gran escala (2021)",
            new[] { new GameVariant("Battlefield 2042", "Battlefield/Battlefield 2042.ps1") }),
        new GameProfile("Battlefield 6",             "Combates a gran escala (2025)",
            new[] { new GameVariant("Battlefield 6", "Battlefield/Battlefield 6.ps1") }),
        new GameProfile("Delta Force",               "FPS militar moderno",
            new[] { new GameVariant("Delta Force", "Delta Force/Delta Force.ps1") }),
        new GameProfile("CoD: Black Ops 4",          "FPS de Activision (2018)",
            new[] { new GameVariant("Black Ops 4", "Call of Duty/Black Ops 4.ps1") }),
        new GameProfile("CoD: Modern Warfare",       "FPS de Activision (2019)",
            new[] { new GameVariant("Modern Warfare", "Call of Duty/Modern Warfare.ps1") }),
        new GameProfile("CoD: Black Ops Cold War",   "FPS de Activision (2020)",
            new[] { new GameVariant("Black Ops Cold War", "Call of Duty/Black Ops Cold War.ps1") }),
        new GameProfile("CoD: Vanguard",             "FPS de Activision (2021)",
            new[] { new GameVariant("Vanguard", "Call of Duty/Vanguard.ps1") }),
        new GameProfile("CoD: Modern Warfare 2",     "FPS de Activision (2022)",
            new[] { new GameVariant("Modern Warfare 2", "Call of Duty/Modern Warfare 2.ps1") }),
        new GameProfile("CoD: Modern Warfare 3",     "FPS de Activision (2023)",
            new[] { new GameVariant("Modern Warfare 3", "Call of Duty/Modern Warfare 3.ps1") }),
        new GameProfile("CoD: Black Ops 6 / Warzone","FPS de Activision (2024)",
            new[] { new GameVariant("Black Ops 6 / Warzone", "Call of Duty/Black Ops 6.ps1") }),
        new GameProfile("CoD: Black Ops 7",          "FPS de Activision (2025)",
            new[] { new GameVariant("Black Ops 7", "Call of Duty/Black Ops 7.ps1") }),
        new GameProfile("Marvel Rivals",             "Hero shooter 6v6",
            new[] { new GameVariant("Marvel Rivals", "Marvel Rivals/Marvel Rivals.ps1") }),
        new GameProfile("Counter Strike 2",          "FPS competitivo táctico",
            new[] { new GameVariant("Counter Strike 2", "Counter Strike 2/Counter Strike 2.ps1") }),
        new GameProfile("Star Wars Battlefront",     "Shooter del universo Star Wars",
            new[] { new GameVariant("Star Wars Battlefront", "STAR WARS Battlefront/STAR WARS Battlefront.ps1") }),
    };

    public Task RunGameOptimizationAsync(GameVariant variant, CancellationToken ct = default)
    {
        // EscapeDataString por segmento: codifica espacios y mantiene la
        // forma URL segura sin "corromper" la ruta como avisa SYSLIB0013.
        var encodedPath = string.Join("/", variant.ScriptRelativePath.Split('/')
            .Select(Uri.EscapeDataString));
        var rawUrl = $"{BaseRawUrl}/{encodedPath}";
        _logger.LogInformation("Lanzando optimización para '{Game}' desde {Url}", variant.DisplayName, rawUrl);

        // Comando que descarga + ejecuta el .ps1 dentro de PowerShell.
        // Bypass de ExecutionPolicy porque la app ya corre como admin (UAC
        // garantizado por el manifest); el usuario optó por ejecutar esto.
        // No esperamos a que termine — el script abre su propia consola y el
        // usuario interactúa con ella.
        var psCommand = $"Invoke-WebRequest -UseBasicParsing '{rawUrl}' | Select-Object -ExpandProperty Content | Invoke-Expression";

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName        = "powershell.exe",
                Arguments       = $"-NoProfile -ExecutionPolicy Bypass -Command \"{psCommand.Replace("\"", "\\\"")}\"",
                UseShellExecute = true, // Mostramos la ventana de PowerShell al usuario
                Verb            = "runas", // por si la app no estuviese en admin por alguna razón
                CreateNoWindow  = false,
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo lanzar el script de optimización del juego");
            throw;
        }

        return Task.CompletedTask;
    }
}
