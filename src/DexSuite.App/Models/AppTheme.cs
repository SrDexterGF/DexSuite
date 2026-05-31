namespace DexSuite.App.Models;

/// <summary>
/// Temas visuales disponibles en DexSuite.
/// El orden de los miembros define el orden en el selector de Ajustes.
/// </summary>
public enum AppTheme
{
    /// <summary>Paleta neutra Wpf.Ui Dark sin overrides (disponible en plan Free).</summary>
    Default = 0,

    /// <summary>Cian + violeta cyberpunk (PRO).</summary>
    Cybernetic = 1,

    /// <summary>Rojo carrera + negro profundo (PRO).</summary>
    Redline = 2,

    /// <summary>Monocromo ultraminimalista (PRO).</summary>
    ZeroLag = 3,

    /// <summary>Oro + negro cálido premium (PRO).</summary>
    Midas = 4,

    // ── Temas con paletas evocadoras (sección oculta "Temas 😉") ──
    /// <summary>Valor — rojo/blanco sobre azul muy oscuro.</summary>
    Valor = 100,

    /// <summary>Fortress — azul + amarillo.</summary>
    Fortress = 101,

    /// <summary>Counter — naranja sobre negro.</summary>
    Counter = 102,

    /// <summary>Legends — azul + dorado.</summary>
    Legends = 103,

    /// <summary>Crafter — verde + marrón.</summary>
    Crafter = 104,

    /// <summary>Apex — rojo + cian sobre azul-gris.</summary>
    Apex = 105,

    /// <summary>Guardian — naranja + azul.</summary>
    Guardian = 106,

    /// <summary>Rivals — morado + dorado.</summary>
    Rivals = 107,

    /// <summary>Tenno — azul claro + blanco frío.</summary>
    Tenno = 108,

    /// <summary>Divers — amarillo + negro militar.</summary>
    Divers = 109,
}
