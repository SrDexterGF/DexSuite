using DexSuite.App.Models;

namespace DexSuite.App.Services;

/// <summary>
/// Servicio de historial interno: persiste eventos relevantes en SQLite
/// para mostrarlos dentro de la app (vista "Registro").
/// </summary>
public interface IAppLogService
{
    /// <summary>
    /// Se dispara tras insertar una entrada nueva. Subscriptores deben
    /// hacer el marshalling al Dispatcher por su cuenta.
    /// </summary>
    event EventHandler<LogEntry>? EntryAdded;

    /// <summary>Inserta una entrada con nivel y categoría arbitrarios.</summary>
    Task WriteAsync(AppLogLevel level, AppLogCategory category, string message, string? details = null);

    /// <summary>Atajo: nivel Info.</summary>
    Task InfoAsync(AppLogCategory category, string message, string? details = null);

    /// <summary>Atajo: nivel Success.</summary>
    Task SuccessAsync(AppLogCategory category, string message, string? details = null);

    /// <summary>Atajo: nivel Warning.</summary>
    Task WarningAsync(AppLogCategory category, string message, string? details = null);

    /// <summary>Atajo: nivel Error.</summary>
    Task ErrorAsync(AppLogCategory category, string message, string? details = null);

    /// <summary>Devuelve las últimas <paramref name="max"/> entradas, más recientes primero.</summary>
    Task<IReadOnlyList<LogEntry>> GetRecentAsync(int max = 500);

    /// <summary>Elimina TODAS las entradas. Devuelve cuántas se borraron.</summary>
    Task<int> ClearAllAsync();

    /// <summary>
    /// Exporta el historial a un .txt en la ruta indicada. Devuelve el path final
    /// (puede haberse renombrado para evitar colisión).
    /// </summary>
    Task<string> ExportToTextAsync(string targetPath);
}
