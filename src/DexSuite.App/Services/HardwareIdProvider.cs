using System.Management;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace DexSuite.App.Services;

/// <summary>
/// Implementación de <see cref="IHardwareIdProvider"/> basada en WMI.
///
/// Fuentes (todas estables en un equipo dado):
///   • <c>Win32_Processor.ProcessorId</c>           — identifica la CPU.
///   • <c>Win32_BaseBoard.SerialNumber</c>          — identifica la placa base.
///   • <c>Win32_ComputerSystemProduct.UUID</c>      — UUID del chasis (BIOS/SMBIOS).
///
/// Si alguno falta (algunos OEMs dejan "Default string" o vacío), se sustituye
/// por un literal fijo para que el HWID siga siendo determinista en ese equipo.
/// El resultado se hashea con SHA-256 y se trunca a 20 caracteres Base32.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class HardwareIdProvider : IHardwareIdProvider
{
    private readonly ILogger<HardwareIdProvider> _logger;
    private string? _canonicalCache;

    public HardwareIdProvider(ILogger<HardwareIdProvider> logger) => _logger = logger;

    public string GetHardwareId()
    {
        var canon = GetCanonicalHardwareId();
        // Formato visual: XXXX-XXXX-XXXX-XXXX-XXXX (4 guiones).
        return $"{canon[..4]}-{canon[4..8]}-{canon[8..12]}-{canon[12..16]}-{canon[16..20]}";
    }

    public string GetCanonicalHardwareId()
    {
        if (_canonicalCache is not null) return _canonicalCache;

        var cpuId  = ReadWmiSingle("Win32_Processor",            "ProcessorId");
        var boardId = ReadWmiSingle("Win32_BaseBoard",            "SerialNumber");
        var biosUuid = ReadWmiSingle("Win32_ComputerSystemProduct", "UUID");

        // Concatenamos con separador "|" para evitar colisiones por interpolación.
        var raw = $"CPU={cpuId}|BOARD={boardId}|UUID={biosUuid}";

        // SHA-256 y truncado a 12 bytes (96 bits) → 20 caracteres en Base32 RFC 4648.
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(raw), hash);

        var truncated = hash[..12];
        var b32 = ToBase32(truncated);

        // Base32 estándar usa 0-7 → reemplazamos por O-V para que el alfabeto sea
        // alfabético puro (queda más limpio visualmente). NO se confunde con el
        // alfabeto estándar al desencodear porque no lo desencodeamos: el HWID
        // se usa como string opaco.
        _canonicalCache = b32[..20];
        _logger.LogInformation("HWID generado: {Hwid}", GetHardwareIdMasked(_canonicalCache));
        return _canonicalCache;
    }

    /// <summary>
    /// Lee la primera instancia de la clase WMI indicada y devuelve la propiedad
    /// como string. Si falla (acceso denegado, propiedad inexistente) devuelve
    /// un literal de fallback para mantener el HWID determinista.
    /// </summary>
    private static string ReadWmiSingle(string wmiClass, string property)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher($"SELECT {property} FROM {wmiClass}");
            foreach (ManagementObject obj in searcher.Get())
            {
                var value = obj[property]?.ToString();
                if (!string.IsNullOrWhiteSpace(value) && value != "Default string")
                    return value.Trim();
            }
        }
        catch { /* propiedad inaccesible — caemos al fallback */ }
        return $"NA-{wmiClass}";
    }

    /// <summary>RFC 4648 Base32 (A-Z, 2-7) sin padding.</summary>
    private static string ToBase32(ReadOnlySpan<byte> bytes)
    {
        const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var sb = new StringBuilder((bytes.Length * 8 + 4) / 5);
        int buffer = 0, bitsLeft = 0;
        foreach (var b in bytes)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                bitsLeft -= 5;
                sb.Append(Alphabet[(buffer >> bitsLeft) & 0x1F]);
            }
        }
        if (bitsLeft > 0)
            sb.Append(Alphabet[(buffer << (5 - bitsLeft)) & 0x1F]);
        return sb.ToString();
    }

    /// <summary>Versión enmascarada para logs (no quemar el HWID a fichero).</summary>
    private static string GetHardwareIdMasked(string canon)
        => canon.Length >= 8 ? $"{canon[..4]}-****-****-****-{canon[^4..]}" : "****";
}
