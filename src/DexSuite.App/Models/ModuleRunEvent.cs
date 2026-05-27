namespace DexSuite.App.Models;

/// <summary>
/// Estado de un módulo dentro de una ejecución en curso.
/// </summary>
public enum ModuleRunStatus
{
    /// <summary>Aún no ha empezado / sin ejecución previa en esta sesión.</summary>
    Idle = 0,
    /// <summary>Spinner: el módulo está ejecutándose.</summary>
    Running = 1,
    /// <summary>Checkmark verde: terminó sin errores.</summary>
    Success = 2,
    /// <summary>Triángulo ámbar: el módulo reportó al menos un error.</summary>
    Error = 3,
}
