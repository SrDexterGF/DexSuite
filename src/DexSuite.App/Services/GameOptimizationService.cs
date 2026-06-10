using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
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

    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

    private readonly ILogger<GameOptimizationService> _logger;

    public GameOptimizationService(ILogger<GameOptimizationService> logger)
    {
        _logger = logger;
    }

    // Cada variante es su propio perfil con una sola entrada → no hay ComboBox en la UI.
    public IReadOnlyList<GameProfile> AvailableGames { get; } = new[]
    {
        new GameProfile("PUBG Battlegrounds",        "Battle royale en tercera persona",
            new[] { new GameVariant("PUBG Battlegrounds", "PUBG BATTLEGROUNDS/PUBG BATTLEGROUNDS.ps1",
                "9455c0853dc7b4558ff355cc29a103e81ed90aea7a6c4476c6be3dae79069f11") }),
        new GameProfile("Frag Punk",                 "Shooter por habilidades",
            new[] { new GameVariant("Frag Punk", "Frag Punk/Frag Punk.ps1",
                "86a9516d939aebe33fc51d60124e5a03258b3a1d3e3968ea5d2d0936f986ffd5") }),
        new GameProfile("Splitgate 1",               "Arena shooter con portales (original)",
            new[] { new GameVariant("Splitgate 1", "Splitgate/Splitgate 1.ps1",
                "c62192e08db797ecbb25675e252fe6a55709c14ee9286ba5593b35e6ed8f8354") }),
        new GameProfile("Splitgate 2",               "Arena shooter con portales (secuela)",
            new[] { new GameVariant("Splitgate 2", "Splitgate/Splitgate 2.ps1",
                "9c55cf5c235beba1be2bb08c24cf2f9661208d96750eaddffd20b8c509e784c9") }),
        new GameProfile("The Finals",                "FPS con destrucción de escenarios",
            new[] { new GameVariant("The Finals", "The Finals/The Finals.ps1",
                "e5f6db65922ed553a0e589fa481bacc882f4b362e99f42a8df1ec071880b9033") }),
        new GameProfile("ARC Raiders",               "Extraction shooter PvPvE",
            new[] { new GameVariant("ARC Raiders", "ARC Raiders/ARC Raiders.ps1",
                "b79690867089aa5286c3310f6926a101481172ac4fe0615f3bffd052c644d2af") }),
        new GameProfile("Battlefield Bad Company 2", "Combates a gran escala (2010)",
            new[] { new GameVariant("Battlefield Bad Company 2", "Battlefield/Battlefield Bad Company 2.ps1",
                "959d8c135c618d81ec56bc08104122126b5a4933f7162bf9cd67bef2d9617667") }),
        new GameProfile("Battlefield 3",             "Combates a gran escala (2011)",
            new[] { new GameVariant("Battlefield 3", "Battlefield/Battlefield 3.ps1",
                "d6be552ff2a390f139faff9e430eaca615072e0d765137f222f76f46b9ed6681") }),
        new GameProfile("Battlefield 4",             "Combates a gran escala (2013)",
            new[] { new GameVariant("Battlefield 4", "Battlefield/Battlefield 4.ps1",
                "d6ff41179a462343273d1dd4f0aa9006fc0ad3bb0f27c4cd3640462bcd69491f") }),
        new GameProfile("Battlefield Hardline",      "Combates a gran escala (2015)",
            new[] { new GameVariant("Battlefield Hardline", "Battlefield/Battlefield Hardline.ps1",
                "a517cd828f676544f390768d3449ca2b66718a2855dd0a43a65fdc0a410389a0") }),
        new GameProfile("Battlefield 1",             "Combates a gran escala (2016)",
            new[] { new GameVariant("Battlefield 1", "Battlefield/Battlefield 1.ps1",
                "8eed1098b753e7b4e002186cda36aac2ed4bb1f3c26a29b2ac2249bfad1eadcd") }),
        new GameProfile("Battlefield V",             "Combates a gran escala (2018)",
            new[] { new GameVariant("Battlefield V", "Battlefield/Battlefield V.ps1",
                "f7e03572b2812c423baffd2f7cc1d3110fdb0e9c83867e7cc8b6fe446e7162fb") }),
        new GameProfile("Battlefield 2042",          "Combates a gran escala (2021)",
            new[] { new GameVariant("Battlefield 2042", "Battlefield/Battlefield 2042.ps1",
                "3b2a513e918f13a79890b1a2f0de037b4aa0378b0ec2bcc36df70f0a196c88aa") }),
        new GameProfile("Battlefield 6",             "Combates a gran escala (2025)",
            new[] { new GameVariant("Battlefield 6", "Battlefield/Battlefield 6.ps1",
                "03f8e5c6044c48af12d6ab21c6d45874403f3dbe31aa4b9a559184618d68ef53") }),
        new GameProfile("Delta Force",               "FPS militar moderno",
            new[] { new GameVariant("Delta Force", "Delta Force/Delta Force.ps1",
                "9f23f5aa4410ece8ad4dd097c0ff04fcf16f88a90c9cbfd4c371b141b22b1aa6") }),
        // Scripts aún no publicados en Game_Configs — hash null hasta que existan.
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
            new[] { new GameVariant("Marvel Rivals", "Marvel Rivals/Marvel Rivals.ps1",
                "f52c60b62af8441fe75d546744ac066108f6d204da47599241ac04060fee6fcd") }),
        new GameProfile("Counter Strike 2",          "FPS competitivo táctico",
            new[] { new GameVariant("Counter Strike 2", "Counter Strike 2/Counter Strike 2.ps1",
                "aa8c9bfafa3eb57ef9bca48a207c1ed44dc97bbb32fdf0745beb9d098505a069") }),
        new GameProfile("Star Wars Battlefront",     "Shooter del universo Star Wars",
            new[] { new GameVariant("Star Wars Battlefront", "STAR WARS Battlefront/STAR WARS Battlefront.ps1") }),
    };

    public async Task RunGameOptimizationAsync(GameVariant variant, CancellationToken ct = default)
    {
        var encodedPath = string.Join("/", variant.ScriptRelativePath.Split('/')
            .Select(Uri.EscapeDataString));
        var rawUrl = $"{BaseRawUrl}/{encodedPath}";
        _logger.LogInformation("Descargando script para '{Game}' desde {Url}", variant.DisplayName, rawUrl);

        byte[] scriptBytes;
        try
        {
            scriptBytes = await _http.GetByteArrayAsync(rawUrl, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo descargar el script de {Game}", variant.DisplayName);
            throw;
        }

        // Verificar SHA-256 si hay hash conocido en el catálogo.
        // Null = hash pendiente de poblar; se ejecuta con advertencia en el log.
        if (variant.ExpectedScriptSha256 is not null)
        {
            var actualHash = Convert.ToHexString(SHA256.HashData(scriptBytes));
            if (!string.Equals(actualHash, variant.ExpectedScriptSha256, StringComparison.OrdinalIgnoreCase))
            {
                var msg = $"Hash del script no coincide para '{variant.DisplayName}'. La descarga puede haber sido manipulada.";
                _logger.LogError(msg);
                throw new InvalidOperationException(msg);
            }
        }
        else
        {
            _logger.LogWarning("Script de '{Game}' ejecutado sin verificación de hash (pendiente de poblar).", variant.DisplayName);
        }

        // Escribir en archivo temporal y ejecutar con -File en lugar de Invoke-Expression.
        var tempFile = Path.Combine(Path.GetTempPath(), $"dexsuite_{Guid.NewGuid():N}.ps1");
        await File.WriteAllBytesAsync(tempFile, scriptBytes, ct).ConfigureAwait(false);

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName        = "powershell.exe",
                Arguments       = $"-NoProfile -ExecutionPolicy Bypass -File \"{tempFile}\"",
                UseShellExecute = true,
                CreateNoWindow  = false,
            };
            var proc = Process.Start(psi);
            // Limpiar el archivo temporal cuando el proceso hijo termine.
            if (proc is not null)
            {
                proc.EnableRaisingEvents = true;
                proc.Exited += (_, _) => { try { File.Delete(tempFile); } catch { } };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo lanzar el script de optimización del juego");
            try { File.Delete(tempFile); } catch { }
            throw;
        }
    }
}
