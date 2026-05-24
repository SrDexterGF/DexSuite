using System.ComponentModel.DataAnnotations;

namespace DexSuite.App.Models;

/// <summary>
/// Niveles de un evento registrado en el historial interno.
/// Determina el color del badge y el icono en la vista.
/// </summary>
public enum AppLogLevel
{
    Info    = 0,
    Success = 1,
    Warning = 2,
    Error   = 3,
}

/// <summary>
/// Origen funcional del evento registrado.
/// Permite filtrar y agrupar el historial por área.
/// </summary>
public enum AppLogCategory
{
    App        = 0,
    Run        = 1,
    QuickClean = 2,
    Analyze    = 3,
    Update     = 4,
    Settings   = 5,
    Language   = 6,
}

/// <summary>
/// Entrada del historial interno persistida en SQLite.
/// Cada acción relevante del usuario (Ejecutar, Limpieza rápida, Test de
/// rendimiento, Actualización, etc.) genera una entrada de este tipo.
/// </summary>
public class LogEntry
{
    public int Id { get; set; }

    /// <summary>Momento del evento (UTC).</summary>
    public DateTime TimestampUtc { get; set; }

    /// <summary>Severidad del evento.</summary>
    public AppLogLevel Level { get; set; }

    /// <summary>Origen funcional del evento.</summary>
    public AppLogCategory Category { get; set; }

    /// <summary>Mensaje corto ya localizado en el momento del evento.</summary>
    [MaxLength(500)]
    public string Message { get; set; } = string.Empty;

    /// <summary>Detalle opcional (stack trace, paths, módulos, etc.).</summary>
    public string? Details { get; set; }
}
