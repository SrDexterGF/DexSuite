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
///   • Build Release con clave pero sin .integrity → devuelve true (warning en log).
///   • .integrity presente pero firma inválida o hash distinto → falla.
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

        // Usamos el DLL (no el .exe) porque Velopack puede ejecutar el app a través
        // de un stub nativo (DexSuite.exe), cuya ruta devuelve Environment.ProcessPath.
        // Assembly.Location apunta siempre al DLL real en current\, sin importar el stub.
        var dllPath = GetAssemblyPath();
        if (dllPath is null || !File.Exists(dllPath))
        {
            reason = "No se pudo localizar el ensamblado principal (DexSuite.App.dll).";
            return false;
        }

        var integrityPath = dllPath + ".integrity";
        if (!File.Exists(integrityPath))
        {
            // .integrity ausente → el ensamblado no pasó por el pipeline de release
            // (p. ej. bin\Release\ durante desarrollo local). Solo bloqueamos si el
            // archivo EXISTE pero la firma o el hash fallan; la ausencia total se
            // trata como "no empaquetado" y no como sabotaje.
            _logger.LogWarning(
                "IntegrityVerifier: .integrity no encontrado junto a {Dll} — verificación omitida.",
                dllPath);
            return true;
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
            using var fs = File.OpenRead(dllPath);
            actualHash = SHA256.HashData(fs);
        }
        catch (Exception ex)
        {
            reason = $"No se pudo leer el ensamblado: {ex.Message}";
            return false;
        }

        if (!CryptographicOperations.FixedTimeEquals(expectedHash, actualHash))
        {
            reason = "El ensamblado ha sido modificado (hash distinto al firmado).";
            return false;
        }

        return true;
#endif
    }

    /// <summary>
    /// Ruta absoluta de <c>DexSuite.App.dll</c>.
    /// Usar Assembly.Location en lugar de Environment.ProcessPath porque Velopack
    /// puede lanzar la app desde un stub nativo y ProcessPath apuntaría al stub,
    /// no al DLL que contiene el código C#.
    /// </summary>
    private static string? GetAssemblyPath()
    {
        try
        {
            var loc = typeof(IntegrityVerifier).Assembly.Location;
            if (!string.IsNullOrEmpty(loc) && File.Exists(loc)) return loc;
        }
        catch { /* sigue al fallback */ }

        // Fallback: construir desde AppContext.BaseDirectory
        try
        {
            var fallback = Path.Combine(AppContext.BaseDirectory, "DexSuite.App.dll");
            return File.Exists(fallback) ? fallback : null;
        }
        catch { return null; }
    }
}
