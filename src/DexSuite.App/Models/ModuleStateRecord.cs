namespace DexSuite.App.Models;

/// <summary>
/// Estado persistente de aplicación de un módulo del catálogo.
/// Sobrevive entre sesiones: la UI lo usa para decidir el indicador del módulo
/// (barra = no aplicado, círculo = aplicando, tick = aplicado).
///
/// Se mantiene una fila por módulo (clave primaria = Id del catálogo).
/// </summary>
public class ModuleStateRecord
{
    /// <summary>Id del módulo en el catálogo (1..19). Clave primaria.</summary>
    public int ModuleId { get; set; }

    /// <summary>True si el módulo ya se ha aplicado con éxito al menos una vez.</summary>
    public bool IsApplied { get; set; }

    /// <summary>Momento del último éxito (UTC), o null si nunca se aplicó.</summary>
    public DateTime? AppliedAtUtc { get; set; }
}
