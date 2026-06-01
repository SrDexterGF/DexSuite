using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DexSuite.KeyGen;

// La herramienta solo se ejecuta en Windows (DPAPI / ProtectedData).
[SupportedOSPlatform("windows")]

/// <summary>
/// Punto de entrada de la herramienta. Cada subcomando es un método estático
/// con su propia validación de argumentos para mantener el binario pequeño
/// y la lógica fácil de auditar.
/// </summary>
public static class Program
{
    private static readonly string KeyDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                     "DexSuiteKeyGen");
    private static readonly string PrivateKeyPath = Path.Combine(KeyDir, "private.xml");

    public static int Main(string[] args)
    {
        if (args.Length == 0) { PrintUsage(); return 1; }

        try
        {
            return args[0].ToLowerInvariant() switch
            {
                "init"           => CmdInit(),
                "pubkey"         => CmdPubKey(args),
                "gen"            => CmdGen(args),
                "sign-integrity" => CmdSignIntegrity(args),
                "verify"         => CmdVerify(args),
                "-h" or "--help" => PrintUsageAndExit(0),
                _                => PrintUsageAndExit(1),
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: {ex.Message}");
            return 2;
        }
    }

    // ── init ────────────────────────────────────────────────────────────

    /// <summary>Crea un par RSA-2048 si no existe y lo persiste cifrado con DPAPI.</summary>
    private static int CmdInit()
    {
        Directory.CreateDirectory(KeyDir);
        if (File.Exists(PrivateKeyPath))
        {
            Console.Error.WriteLine($"Ya existe una clave privada en:");
            Console.Error.WriteLine($"  {PrivateKeyPath}");
            Console.Error.WriteLine("Sobrescribir invalidaría TODAS las licencias emitidas.");
            Console.Error.WriteLine("Borra el archivo a mano si estás seguro de que quieres regenerar.");
            return 3;
        }

        using var rsa = RSA.Create(2048);
        var xml = rsa.ToXmlString(includePrivateParameters: true);
        SaveEncryptedPrivateKey(xml);

        // Permisos: solo el usuario actual (sin esto, Documents/AppData son legibles
        // por procesos en el mismo perfil — admin sí puede leerlo igual).
        try
        {
            var fi = new FileInfo(PrivateKeyPath) { Attributes = FileAttributes.Hidden };
            // En NTFS quedan los ACL heredados; con Hidden + carpeta del usuario es suficiente
            // para uso del desarrollador. Para máxima seguridad: guardar en un USB cifrado.
        }
        catch { /* atributo opcional */ }

        Console.WriteLine($"Clave privada generada en:");
        Console.WriteLine($"  {PrivateKeyPath}");
        Console.WriteLine();
        Console.WriteLine("Siguiente paso recomendado:");
        Console.WriteLine("  DexSuite.KeyGen pubkey --update-app \"<ruta_a_DexSuite.App>\"");
        Console.WriteLine();
        Console.WriteLine("AVISO: si pierdes este archivo no podrás generar más licencias NI");
        Console.WriteLine("regenerar .integrity para nuevas releases. Haz copia de seguridad.");
        return 0;
    }

    // ── pubkey ──────────────────────────────────────────────────────────

    /// <summary>Imprime la clave pública en Base64-SPKI partida en 4 trozos.</summary>
    private static int CmdPubKey(string[] args)
    {
        using var rsa = LoadPrivateKey();
        var spki = rsa.ExportSubjectPublicKeyInfo();
        var b64  = Convert.ToBase64String(spki);

        // Reparto equitativo en 4 partes — la longitud del Base64 SPKI de
        // RSA-2048 ronda los 400 caracteres, así que cada parte es ~100.
        var partLen = (b64.Length + 3) / 4;
        var parts = new string[4];
        for (int i = 0; i < 4; i++)
        {
            var start = i * partLen;
            var len = Math.Min(partLen, b64.Length - start);
            parts[i] = len > 0 ? b64.Substring(start, len) : string.Empty;
        }

        var updateApp = ParseFlag(args, "--update-app");
        if (updateApp is null)
        {
            Console.WriteLine("Clave pública (Base64 SPKI) repartida en 4 partes:");
            Console.WriteLine();
            for (int i = 0; i < 4; i++)
            {
                Console.WriteLine($"KeyPart{(char)('A' + i)}.Value = \"{parts[i]}\";");
            }
            Console.WriteLine();
            Console.WriteLine("Para escribirlos automáticamente:");
            Console.WriteLine("  DexSuite.KeyGen pubkey --update-app \"<ruta_a_src/DexSuite.App>\"");
            return 0;
        }

        WriteKeyParts(updateApp, parts);
        Console.WriteLine($"4 archivos KeyPart*.cs actualizados en {updateApp}");
        return 0;
    }

    private static void WriteKeyParts(string appPath, string[] parts)
    {
        var dir = Path.Combine(appPath, "Services", "Licensing", "Keys");
        if (!Directory.Exists(dir))
            throw new DirectoryNotFoundException($"No existe {dir}");

        for (int i = 0; i < 4; i++)
        {
            var letter = (char)('A' + i);
            var file = Path.Combine(dir, $"KeyPart{letter}.cs");
            var content = $$"""
namespace DexSuite.App.Services.Licensing.Keys;

/// <summary>Parte {{letter}} ({{i + 1}}/4) de la clave pública RSA-2048 de DexSuite.</summary>
/// <remarks>Generado por DexSuite.KeyGen pubkey --update-app. No editar a mano.</remarks>
internal static class KeyPart{{letter}}
{
    internal const string Value = "{{parts[i]}}";
}
""";
            File.WriteAllText(file, content, Encoding.UTF8);
        }
    }

    // ── gen ─────────────────────────────────────────────────────────────

    /// <summary>Construye, serializa y firma una licencia para un HWID y tier dados.</summary>
    private static int CmdGen(string[] args)
    {
        var hwid = ParseFlag(args, "--hwid")
            ?? throw new ArgumentException("Falta --hwid <HWID-del-cliente>");
        var tier = ParseFlag(args, "--tier")
            ?? throw new ArgumentException("Falta --tier <Advanced|Pro>");

        // Normalizamos el HWID (quitamos guiones del formato visual).
        hwid = hwid.Replace("-", "").Trim().ToUpperInvariant();
        if (hwid.Length != 20)
            throw new ArgumentException($"HWID inválido: esperado 20 caracteres tras quitar guiones, recibido {hwid.Length}.");

        if (!string.Equals(tier, "Advanced", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(tier, "Pro",      StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("--tier debe ser Advanced o Pro.");

        var payload = new LicensePayloadDto
        {
            Hwid        = hwid,
            Tier        = char.ToUpperInvariant(tier[0]) + tier[1..].ToLowerInvariant(),
            IssuedAtUtc = DateTime.UtcNow,
            LicenseId   = Guid.NewGuid().ToString("N"),
        };

        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload);

        using var rsa = LoadPrivateKey();
        var signature = rsa.SignData(payloadBytes,
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        var blob = Convert.ToBase64String(payloadBytes) + "." + Convert.ToBase64String(signature);

        Console.WriteLine();
        Console.WriteLine($"  HWID      : {payload.Hwid}");
        Console.WriteLine($"  Tier      : {payload.Tier}");
        Console.WriteLine($"  LicenseId : {payload.LicenseId}");
        Console.WriteLine($"  IssuedAt  : {payload.IssuedAtUtc:O}");
        Console.WriteLine();
        Console.WriteLine("Clave de activación (envíala al cliente):");
        Console.WriteLine();
        Console.WriteLine(blob);
        Console.WriteLine();
        return 0;
    }

    // ── sign-integrity ──────────────────────────────────────────────────

    /// <summary>Firma el SHA-256 del exe pasado y escribe el archivo .integrity hermano.</summary>
    private static int CmdSignIntegrity(string[] args)
    {
        if (args.Length < 2)
            throw new ArgumentException("Uso: DexSuite.KeyGen sign-integrity <ruta-al-exe>");

        var exePath = args[1];
        if (!File.Exists(exePath))
            throw new FileNotFoundException($"No existe: {exePath}");

        byte[] hash;
        using (var fs = File.OpenRead(exePath))
            hash = SHA256.HashData(fs);

        using var rsa = LoadPrivateKey();
        var signature = rsa.SignData(hash,
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        var integrityPath = exePath + ".integrity";
        var content = Convert.ToBase64String(hash) + "." + Convert.ToBase64String(signature);
        File.WriteAllText(integrityPath, content, Encoding.UTF8);

        Console.WriteLine($"  SHA-256  : {Convert.ToHexString(hash)}");
        Console.WriteLine($"  Firma OK → {integrityPath}");
        return 0;
    }

    // ── verify (debug) ──────────────────────────────────────────────────

    /// <summary>Útil para depurar: verifica una clave de activación localmente.</summary>
    private static int CmdVerify(string[] args)
    {
        if (args.Length < 2)
            throw new ArgumentException("Uso: DexSuite.KeyGen verify <blob>");

        var blob = args[1];
        var parts = blob.Split('.', 2);
        if (parts.Length != 2) throw new ArgumentException("Blob mal formado.");

        var payloadBytes = Convert.FromBase64String(parts[0]);
        var signature    = Convert.FromBase64String(parts[1]);

        using var rsa = LoadPrivateKey(); // verificación con par privado (igualmente válido)
        var ok = rsa.VerifyData(payloadBytes, signature,
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        var payload = JsonSerializer.Deserialize<LicensePayloadDto>(payloadBytes);
        Console.WriteLine($"  Firma     : {(ok ? "VÁLIDA" : "INVÁLIDA")}");
        Console.WriteLine($"  HWID      : {payload?.Hwid}");
        Console.WriteLine($"  Tier      : {payload?.Tier}");
        Console.WriteLine($"  LicenseId : {payload?.LicenseId}");
        Console.WriteLine($"  IssuedAt  : {payload?.IssuedAtUtc:O}");
        return ok ? 0 : 4;
    }

    // ── helpers ─────────────────────────────────────────────────────────

    /// <summary>
    /// Carga la clave privada del disco. El archivo está cifrado con DPAPI
    /// (CurrentUser scope): solo el mismo usuario de Windows que lo creó puede
    /// descifrarlo. Si el archivo existe sin cifrar (versión anterior), lo
    /// cifra in-place antes de devolver — migración automática y transparente.
    /// </summary>
    private static RSA LoadPrivateKey()
    {
        if (!File.Exists(PrivateKeyPath))
            throw new FileNotFoundException(
                $"No hay clave privada. Ejecuta primero: DexSuite.KeyGen init",
                PrivateKeyPath);

        var raw = File.ReadAllBytes(PrivateKeyPath);
        string xml;

        if (IsPlainXml(raw))
        {
            // Archivo legado en claro — leer, cifrar in-place y continuar.
            xml = Encoding.UTF8.GetString(raw).TrimStart('﻿');
            Console.WriteLine("Detectada clave en claro; migrando a cifrado DPAPI...");
            SaveEncryptedPrivateKey(xml);
            Console.WriteLine("Migración completada.");
        }
        else
        {
            var plain = ProtectedData.Unprotect(raw, optionalEntropy: null,
                scope: DataProtectionScope.CurrentUser);
            xml = Encoding.UTF8.GetString(plain).TrimStart('﻿');
        }

        var rsa = RSA.Create();
        rsa.FromXmlString(xml);
        return rsa;
    }

    /// <summary>Escribe la clave cifrada con DPAPI (CurrentUser).</summary>
    private static void SaveEncryptedPrivateKey(string xml)
    {
        var plain = Encoding.UTF8.GetBytes(xml);
        var encrypted = ProtectedData.Protect(plain, optionalEntropy: null,
            scope: DataProtectionScope.CurrentUser);

        // Si existe con Hidden / ReadOnly, WriteAllBytes da Access Denied.
        // Limpiamos atributos antes de sobrescribir.
        if (File.Exists(PrivateKeyPath))
            File.SetAttributes(PrivateKeyPath, FileAttributes.Normal);

        File.WriteAllBytes(PrivateKeyPath, encrypted);

        try { File.SetAttributes(PrivateKeyPath, FileAttributes.Hidden); }
        catch { /* atributo opcional */ }
    }

    /// <summary>
    /// Heurística: los blobs DPAPI empiezan por bytes binarios; un XML plano
    /// empieza siempre por '&lt;' (0x3C), opcionalmente con BOM UTF-8 (EF BB BF).
    /// </summary>
    private static bool IsPlainXml(byte[] raw)
    {
        if (raw.Length == 0) return false;
        int offset = 0;
        if (raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF)
            offset = 3;
        return offset < raw.Length && raw[offset] == (byte)'<';
    }

    private static string? ParseFlag(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        return null;
    }

    private static int PrintUsageAndExit(int code) { PrintUsage(); return code; }

    private static void PrintUsage()
    {
        Console.WriteLine("DexSuite.KeyGen — gestión de claves del sistema de licencias.");
        Console.WriteLine();
        Console.WriteLine("Comandos:");
        Console.WriteLine("  init");
        Console.WriteLine("      Genera el par RSA-2048 y lo guarda en %LocalAppData%\\DexSuiteKeyGen.");
        Console.WriteLine();
        Console.WriteLine("  pubkey [--update-app <ruta-a-src/DexSuite.App>]");
        Console.WriteLine("      Imprime la clave pública partida en 4. Con --update-app sobreescribe");
        Console.WriteLine("      automáticamente los archivos KeyPart*.cs del proyecto.");
        Console.WriteLine();
        Console.WriteLine("  gen --hwid <HWID> --tier <Advanced|Pro>");
        Console.WriteLine("      Genera una clave de activación firmada para un cliente.");
        Console.WriteLine();
        Console.WriteLine("  sign-integrity <ruta-al-exe>");
        Console.WriteLine("      Firma el SHA-256 del exe y escribe <exe>.integrity (CAPA 2).");
        Console.WriteLine();
        Console.WriteLine("  verify <blob>");
        Console.WriteLine("      Depura una clave de activación localmente.");
    }

    /// <summary>
    /// Versión local del payload para evitar depender de DexSuite.App. Debe
    /// mantenerse en sync (mismos JsonPropertyName).
    /// </summary>
    private sealed class LicensePayloadDto
    {
        [JsonPropertyName("hwid")]      public string Hwid { get; set; } = "";
        [JsonPropertyName("tier")]      public string Tier { get; set; } = "Free";
        [JsonPropertyName("issuedAt")]  public DateTime IssuedAtUtc { get; set; }
        [JsonPropertyName("licenseId")] public string LicenseId { get; set; } = "";
    }
}
