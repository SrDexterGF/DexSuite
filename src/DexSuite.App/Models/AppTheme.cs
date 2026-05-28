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
    Apex = 4,
}
