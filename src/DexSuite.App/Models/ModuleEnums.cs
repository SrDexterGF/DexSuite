namespace DexSuite.App.Models;

/// <summary>
/// Agrupacion logica de los modulos, paralela a los bloques del .bat original
/// (LIMPIEZA / AJUSTES / HARDWARE / EXTRAS).
/// </summary>
public enum ModuleCategory
{
    Cleanup,
    Settings,
    Hardware,
    Extras,
}

/// <summary>
/// Nivel de licencia requerido para ejecutar el modulo.
/// </summary>
public enum ModuleTier
{
    Free,
    Advanced,
    Pro,
}
