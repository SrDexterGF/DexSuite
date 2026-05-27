using System.ComponentModel.DataAnnotations;

namespace DexSuite.App.Models;

/// <summary>
/// Tipo de cambio aplicado por un módulo nativo de DexSuite.
/// Determina cómo se revierte el cambio.
/// </summary>
public enum ChangeType
{
    /// <summary>Valor del registro de Windows (HKLM/HKCU/etc.).</summary>
    RegistryValue   = 0,

    /// <summary>Clave entera del registro (creada o eliminada).</summary>
    RegistryKey     = 1,

    /// <summary>Tipo de inicio de un servicio Windows (Auto/Manual/Disabled).</summary>
    ServiceStartup  = 2,

    /// <summary>Tarea programada habilitada o deshabilitada.</summary>
    ScheduledTask   = 3,

    /// <summary>Archivo creado, modificado o eliminado.</summary>
    FileSystem      = 4,
}

/// <summary>
/// Registro de un cambio individual aplicado por un módulo nativo.
/// Permite revertir cambios de forma granular sin depender de System Restore.
///
/// IMPORTANTE: solo los módulos C# nativos generan estos registros.
/// Para cambios que no aparezcan aquí, usa System Restore como red de seguridad.
/// </summary>
public class ModuleChangeRecord
{
    public int Id { get; set; }

    /// <summary>Identificador del módulo que originó el cambio (ModuleCatalog.Key).</summary>
    [MaxLength(80)]
    public string ModuleId { get; set; } = string.Empty;

    /// <summary>Nombre legible del módulo en el momento de aplicar el cambio.</summary>
    [MaxLength(200)]
    public string ModuleName { get; set; } = string.Empty;

    /// <summary>Tipo de cambio (determina el método de reversión).</summary>
    public ChangeType ChangeType { get; set; }

    /// <summary>
    /// Objetivo del cambio (clave de registro, ruta del servicio, ruta de archivo).
    /// Formato dependiente de ChangeType.
    /// </summary>
    [MaxLength(500)]
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// Sub-objetivo opcional (p. ej. nombre del valor del registro dentro de la clave).
    /// </summary>
    [MaxLength(200)]
    public string? SubTarget { get; set; }

    /// <summary>Valor original (serializado como string). null si el target no existía antes.</summary>
    public string? OriginalValue { get; set; }

    /// <summary>Valor nuevo aplicado (serializado como string).</summary>
    public string? NewValue { get; set; }

    /// <summary>Tipo del valor (DWORD, SZ, MULTI_SZ, etc.). Solo para registro.</summary>
    [MaxLength(20)]
    public string? ValueKind { get; set; }

    /// <summary>Momento en que se aplicó el cambio (UTC).</summary>
    public DateTime AppliedAtUtc { get; set; }

    /// <summary>True si el cambio ya ha sido revertido.</summary>
    public bool IsReverted { get; set; }

    /// <summary>Momento en que se revirtió el cambio (UTC), si aplica.</summary>
    public DateTime? RevertedAtUtc { get; set; }

    /// <summary>Mensaje de error del último intento de reversión, si falló.</summary>
    [MaxLength(500)]
    public string? RevertError { get; set; }
}
