using System.Text.Json.Serialization;

namespace DexSuite.App.Models;

/// <summary>
/// Cuerpo firmable de una licencia de DexSuite. Se serializa a JSON canónico
/// (mismas claves, mismo orden, sin espacios) en la herramienta dev, se firma
/// con RSA-SHA256 y la app vuelve a serializar igual para verificar la firma.
///
/// Sin Expiry → licencia perpetua (decisión Bloque 6: sin caducidad).
/// </summary>
public sealed class LicensePayload
{
    /// <summary>HWID al que está vinculada (debe casar con el actual al activar y revalidar).</summary>
    [JsonPropertyName("hwid")]
    public string Hwid { get; set; } = string.Empty;

    /// <summary>"Advanced" o "Pro" (en string para que el JSON sea legible).</summary>
    [JsonPropertyName("tier")]
    public string Tier { get; set; } = "Free";

    /// <summary>Fecha de emisión (UTC, ISO 8601).</summary>
    [JsonPropertyName("issuedAt")]
    public DateTime IssuedAtUtc { get; set; }

    /// <summary>Guid único de esta licencia (futuro: listas de revocación).</summary>
    [JsonPropertyName("licenseId")]
    public string LicenseId { get; set; } = Guid.NewGuid().ToString("N");
}
