namespace DexSuite.App.Models;

/// <summary>
/// Una variante concreta de un juego (p. ej. "Battlefield 4" dentro de la
/// familia Battlefield). Cada variante mapea a un fichero .ps1 exacto en el
/// repositorio Game_Configs.
/// </summary>
/// <param name="DisplayName">Nombre legible que aparece en el desplegable.</param>
/// <param name="ScriptRelativePath">Ruta dentro del repo Game_Configs hasta el .ps1, p. ej. "Battlefield/Battlefield 4.ps1".</param>
public sealed record GameVariant(string DisplayName, string ScriptRelativePath);

/// <summary>
/// Perfil de un juego soportado por Game_Configs. Sincronizado a mano con
/// IWR.ps1 del repositorio público — al añadir un juego nuevo en GitHub, hay
/// que añadir su entrada aquí.
/// </summary>
/// <param name="Name">Nombre canónico (mismo que la carpeta del repo).</param>
/// <param name="Subtitle">Frase corta tipo "FPS competitivo / Battle Royale".</param>
/// <param name="Variants">Una o varias versiones del juego, cada una con su .ps1.</param>
public sealed record GameProfile(
    string Name,
    string Subtitle,
    IReadOnlyList<GameVariant> Variants);
