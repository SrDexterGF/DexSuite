using System.IO;
using System.Security.Cryptography;
using DexSuite.App.Services.Licensing;
using Microsoft.Extensions.Logging;

namespace DexSuite.App.Services;

/// <summary>
/// Implementación de <see cref="IIntegrityVerifier"/>.
///
/// Flujo (solo en Release):
///   1. Localiza el path del .exe actual (<c>AppContext.BaseDirectory</c> + nombre).
///   2. Lee el sibling <c>&lt;exe&gt;.integrity</c>: formato "Base64(hash).Base64(firma)".
///   3. Verifica la firma RSA-SHA256 de los bytes del hash con la clave pública.
///   4. Calcula SHA-256 del .exe actual y lo compara con el hash firmado.
///
/// Comportamiento:
///   • Build Debug → devuelve true siempre (no bloquea el F5 desde Visual Studio).
///   • Build Release sin clave pública embebida (placeholder) → devuelve true.
///   • Build Release con clave pero sin .integrity → falla.
///   • Firma inválida o hash distinto → falla.
/// </summary>
public sealed class IntegrityVerifier : IIntegrityVerifier
{
    private readonly ILogger<IntegrityVerifier> _logger;

    public IntegrityVerifier(ILogger<IntegrityVerifier> logger) => _logger = logger;

    public bool Verify(out string reason)
    {
        reason = string.Empty;

#if DEBUG
        _logger.LogInformation("IntegrityVerifier: build Debug, verificación omitida.");
        return true;
#else
        using var rsa = PublicKeyAssembler.TryCreatePublicKey();
        if (rsa is null)
        {
            _logger.LogInformation(
                "IntegrityVerifier: clave pública no configurada, verificación omitida.");
            return true;
        }

        var exePath = GetExecutablePath();
        if (exePath is null || !File.Exists(exePath))
        {
            reason = "No se pudo localizar el ejecutable principal";
            return false;
        }

        var integrityPath = exePath + ".integrity";
        if (!File.Exists(integrityPath))
        {
            reason = "Falta el archivo de integridad (.integrity). El binario puede haber sido alterado.";
            return false;
        }

        string integrityContent;
        try { integrityContent = File.ReadAllText(integrityPath).Trim(); }
        catch (Exception ex)
        {
            reason = $"No se pudo leer .integrity: {ex.Message}";
            return false;
        }

        var parts = integrityContent.Split('.', 2);
        if (parts.Length != 2)
        {
            reason = "Formato de .integrity inválido";
            return false;
        }

        byte[] expectedHash, signature;
        try
        {
            expectedHash = Convert.FromBase64String(parts[0]);
            signature    = Convert.FromBase64String(parts[1]);
        }
        catch (FormatException)
        {
            reason = "Contenido de .integrity corrupto (no es Base64)";
            return false;
        }

        bool sigOk;
        try
        {
            sigOk = rsa.VerifyData(
                expectedHash,
                signature,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
        }
        catch (Exception ex)
        {
            reason = $"Excepción al verificar firma: {ex.Message}";
            return false;
        }
        if (!sigOk)
        {
            reason = "La firma del archivo .integrity no es válida.";
            return false;
        }

        byte[] actualHash;
        try
        {
            using var fs = File.OpenRead(exePath);
            actualHash = SHA256.HashData(fs);
        }
        catch (Exception ex)
        {
            reason = $"No se pudo leer el ejecutable: {ex.Message}";
            return false;
        }

        if (!CryptographicOperations.FixedTimeEquals(expectedHash, actualHash))
        {
            reason = "El ejecutable ha sido modificado (hash distinto al firmado).";
            return false;
        }

        return true;
#endif
    }

    /// <summary>
    /// Ruta absoluta del .exe que aloja el proceso actual. Para apps publicadas
    /// con Velopack es <c>%LocalAppData%\DexSuite\current\DexSuite.App.exe</c>.
    /// </summary>
    private static string? GetExecutablePath()
    {
        try
        {
            var path = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(path) && File.Exists(path)) return path;
        }
        catch { /* sigue al fallback */ }

        try
        {
            var dir = AppContext.BaseDirectory;
            var name = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(name) && File.Exists(name)) return name;
            var fallback = Path.Combine(dir, "DexSuite.App.exe");
            return File.Exists(fallback) ? fallback : null;
        }
        catch { return null; }
    }
}
