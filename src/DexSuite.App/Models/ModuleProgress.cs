namespace DexSuite.App.Models;

/// <summary>
/// Tipo de evento emitido por un módulo durante su ejecución nativa.
/// Permite que la UI distinga la cabecera del módulo de los mensajes intermedios
/// (pasos, avisos, errores) y del cierre final.
/// </summary>
public enum ModuleProgressKind
{
    Header,      // Inicio del módulo. La UI usa esto para marcar Running.
    Step,        // Subpaso textual ("--> Borrando Prefetch").
    Ok,          // Subpaso completado correctamente ("[OK] ...").
    Warn,        // Aviso no fatal ("[WARN] ...").
    Error,       // Error puntual dentro del módulo ("[ERROR] ...").
    Info,        // Línea informativa sin estado.
    Heartbeat,   // Pulso de actividad mientras un Process largo no emite stdout.
    Done,        // Cierre del módulo: la UI usa esto para marcar Completed.
}

/// <summary>
/// Evento estructurado emitido por <see cref="Services.CleanupModules.IModuleExecutor"/>.
/// La UI conoce el ModuleId directamente sin necesidad de parsear texto.
/// </summary>
public sealed record ModuleProgress(
    int ModuleId,
    ModuleProgressKind Kind,
    string Message)
{
    public static ModuleProgress Header(int id, string msg)    => new(id, ModuleProgressKind.Header,    msg);
    public static ModuleProgress Step(int id, string msg)      => new(id, ModuleProgressKind.Step,      msg);
    public static ModuleProgress Ok(int id, string msg)        => new(id, ModuleProgressKind.Ok,        msg);
    public static ModuleProgress Warn(int id, string msg)      => new(id, ModuleProgressKind.Warn,      msg);
    public static ModuleProgress Err(int id, string msg)       => new(id, ModuleProgressKind.Error,     msg);
    public static ModuleProgress Info(int id, string msg)      => new(id, ModuleProgressKind.Info,      msg);
    public static ModuleProgress Heartbeat(int id, string msg) => new(id, ModuleProgressKind.Heartbeat, msg);
    public static ModuleProgress Done(int id, string msg)      => new(id, ModuleProgressKind.Done,      msg);
}
