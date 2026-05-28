namespace DexSuite.App.Models;

/// <summary>
/// Agrupación lógica de los módulos nativos de DexSuite
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
