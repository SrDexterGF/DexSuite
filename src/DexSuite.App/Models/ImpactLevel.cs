namespace DexSuite.App.Models;

/// <summary>
/// Nivel de impacto que una opción tiene sobre el rendimiento del sistema.
/// Se usa para el badge visual en cada módulo (gris → verde → amarillo → naranja → rojo).
/// </summary>
public enum ImpactLevel
{
    /// <summary>Sin impacto medible en rendimiento (limpieza inocua, info, etc.).</summary>
    None = 0,

    /// <summary>Impacto suave. Pequeñas mejoras, apenas perceptibles a simple vista.</summary>
    Soft = 1,

    /// <summary>Impacto notable. Mejora visible en uso diario.</summary>
    Notable = 2,

    /// <summary>Impacto fuerte. Mejora significativa, especialmente en equipos modestos.</summary>
    Strong = 3,

    /// <summary>Impacto extremo. Cambios agresivos orientados a FPS máximos y latencia mínima.</summary>
    Extreme = 4,
}
