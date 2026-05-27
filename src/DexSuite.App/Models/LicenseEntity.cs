using System.ComponentModel.DataAnnotations;

namespace DexSuite.App.Models;

/// <summary>
/// Registro persistido en SQLite (tabla Licenses) de la licencia actualmente
/// aplicada. El campo <see cref="Blob"/> guarda la clave de activación tal
/// cual la pegó el usuario; la app la re-verifica desde cero en cada arranque
/// y en cada tick del watchdog (no se confía en <see cref="Tier"/> almacenado).
/// </summary>
public sealed class LicenseEntity
{
    [Key]
    public int Id { get; set; }

    /// <summary>HWID al que se vinculó la licencia (debe coincidir con el del equipo).</summary>
    public string Hwid { get; set; } = string.Empty;

    /// <summary>Tier resuelto al activar (1 = Advanced, 2 = Pro).</summary>
    public int Tier { get; set; }

    /// <summary>Guid único de la licencia (LicensePayload.LicenseId).</summary>
    public string LicenseId { get; set; } = string.Empty;

    /// <summary>Clave de activación completa: "Base64(payload).Base64(firma)".</summary>
    public string Blob { get; set; } = string.Empty;

    /// <summary>Fecha de emisión que firmó el desarrollador.</summary>
    public DateTime IssuedAtUtc { get; set; }

    /// <summary>Fecha en que el usuario aplicó la clave en su equipo.</summary>
    public DateTime AppliedAtUtc { get; set; } = DateTime.UtcNow;
}
