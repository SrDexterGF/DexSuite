using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DexSuite.App.Data;
using DexSuite.App.Models;
using DexSuite.App.Services.Licensing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DexSuite.App.Services;

/// <summary>
/// Implementación de <see cref="ILicenseService"/>.
///
/// Formato de clave de activación:
///     <c>BASE64URL(payloadJsonBytes) "." BASE64URL(rsaSignature)</c>
///
/// - El payload es <see cref="LicensePayload"/> serializado con JsonSerializerOptions
///   por defecto (mismo formato en el dev tool y aquí — System.Text.Json es
///   determinista para tipos planos).
/// - La firma cubre los bytes del payload con RSA-SHA256 + PKCS#1 v1.5.
/// - El HWID dentro del payload debe casar con <see cref="IHardwareIdProvider.GetCanonicalHardwareId"/>.
///
/// La verificación se ejecuta en TODOS los puntos críticos:
///   1. <see cref="ActivateAsync"/> antes de persistir.
///   2. <see cref="RevalidateAsync"/> en cada arranque y tick del watchdog.
///   3. Nunca se confía en <see cref="LicenseEntity.Tier"/> almacenado: se
///      recalcula siempre a partir del blob firmado.
/// </summary>
public sealed class LicenseService : ILicenseService
{
    private readonly IDbContextFactory<DexSuiteDbContext> _dbFactory;
    private readonly IHardwareIdProvider _hwid;
    private readonly ILogger<LicenseService> _logger;

    private ModuleTier _currentTier = ModuleTier.Free;
    private readonly object _stateLock = new();

    public event EventHandler<ModuleTier>? TierChanged;

    public ModuleTier CurrentTier
    {
        get { lock (_stateLock) return _currentTier; }
    }

    public LicenseService(
        IDbContextFactory<DexSuiteDbContext> dbFactory,
        IHardwareIdProvider hwid,
        ILogger<LicenseService> logger)
    {
        _dbFactory = dbFactory;
        _hwid = hwid;
        _logger = logger;
    }

    public string GetHardwareId() => _hwid.GetHardwareId();

    public async Task<LicenseOperationResult> ActivateAsync(string activationKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(activationKey))
            return new LicenseOperationResult(false, ModuleTier.Free, "La clave está vacía");

        var key = activationKey.Trim();
        var verification = VerifyBlob(key);
        if (!verification.IsValid)
        {
            _logger.LogWarning("Intento de activación rechazado: {Reason}", verification.Reason);
            return new LicenseOperationResult(false, ModuleTier.Free, verification.Reason);
        }

        var payload = verification.Payload!;
        var entity = new LicenseEntity
        {
            Hwid         = payload.Hwid,
            Tier         = (int)ParseTier(payload.Tier),
            LicenseId    = payload.LicenseId,
            Blob         = key,
            IssuedAtUtc  = payload.IssuedAtUtc,
            AppliedAtUtc = DateTime.UtcNow,
        };

        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            // Solo una licencia activa: borramos las anteriores.
            db.Licenses.RemoveRange(db.Licenses);
            db.Licenses.Add(entity);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo persistir la licencia en SQLite");
            return new LicenseOperationResult(false, ModuleTier.Free, "Error guardando la licencia");
        }

        var tier = (ModuleTier)entity.Tier;
        SetTier(tier);
        _logger.LogInformation("Licencia activada: tier={Tier}, licenseId={LicenseId}", tier, payload.LicenseId);
        return new LicenseOperationResult(true, tier, null);
    }

    public async Task<LicenseOperationResult> RevalidateAsync(CancellationToken ct = default)
    {
        LicenseEntity? entity;
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            entity = await db.Licenses.AsNoTracking().FirstOrDefaultAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo leer la tabla Licenses");
            SetTier(ModuleTier.Free);
            return new LicenseOperationResult(false, ModuleTier.Free, "No se pudo leer la base de datos");
        }

        if (entity is null)
        {
            SetTier(ModuleTier.Free);
            return new LicenseOperationResult(true, ModuleTier.Free, "Sin licencia activa (Free)");
        }

        var verification = VerifyBlob(entity.Blob);
        if (!verification.IsValid)
        {
            _logger.LogWarning("Re-verificación falló: {Reason} — revertiendo a Free", verification.Reason);
            SetTier(ModuleTier.Free);
            return new LicenseOperationResult(false, ModuleTier.Free, verification.Reason);
        }

        var tier = ParseTier(verification.Payload!.Tier);
        SetTier(tier);
        return new LicenseOperationResult(true, tier, null);
    }

    public async Task DeactivateAsync(CancellationToken ct = default)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            db.Licenses.RemoveRange(db.Licenses);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo eliminar la licencia almacenada");
        }
        SetTier(ModuleTier.Free);
    }

    // ── Verificación ────────────────────────────────────────────────────

    /// <summary>
    /// Resultado intermedio de la verificación de un blob de licencia.
    /// Si IsValid es true, Payload contiene el payload deserializado.
    /// </summary>
    private readonly record struct BlobVerification(bool IsValid, string? Reason, LicensePayload? Payload);

    private BlobVerification VerifyBlob(string blob)
    {
        if (string.IsNullOrWhiteSpace(blob))
            return new BlobVerification(false, "Clave vacía", null);

        // Defensa en profundidad: aunque la clave pública no esté configurada en
        // builds de desarrollo, NUNCA aceptamos una activación sin firma válida.
        using var rsa = PublicKeyAssembler.TryCreatePublicKey();
        if (rsa is null)
            return new BlobVerification(false,
                "Clave pública no configurada en esta build (modo dev). No se pueden activar licencias.", null);

        var parts = blob.Split('.', 2);
        if (parts.Length != 2)
            return new BlobVerification(false, "Formato de clave inválido (falta el separador '.')", null);

        byte[] payloadBytes, signatureBytes;
        try
        {
            payloadBytes   = Convert.FromBase64String(PadBase64(parts[0]));
            signatureBytes = Convert.FromBase64String(PadBase64(parts[1]));
        }
        catch (FormatException)
        {
            return new BlobVerification(false, "Clave corrupta (no es Base64)", null);
        }

        bool sigOk;
        try
        {
            sigOk = rsa.VerifyData(
                payloadBytes,
                signatureBytes,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
        }
        catch (Exception ex)
        {
            return new BlobVerification(false, $"Error al verificar firma: {ex.Message}", null);
        }
        if (!sigOk)
            return new BlobVerification(false, "La firma de la clave no es válida", null);

        LicensePayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<LicensePayload>(payloadBytes);
        }
        catch (Exception ex)
        {
            return new BlobVerification(false, $"Payload de licencia ilegible: {ex.Message}", null);
        }
        if (payload is null)
            return new BlobVerification(false, "Payload vacío", null);

        // HWID: el de dentro del payload debe ser exactamente el de este equipo.
        var canonicalHere = _hwid.GetCanonicalHardwareId();
        if (!string.Equals(payload.Hwid, canonicalHere, StringComparison.Ordinal))
            return new BlobVerification(false,
                "La clave está vinculada a otro equipo (HWID no coincide)", null);

        // Sin caducidad: no comprobamos expiry (decisión de producto).

        return new BlobVerification(true, null, payload);
    }

    private static ModuleTier ParseTier(string tier) => tier?.Trim().ToLowerInvariant() switch
    {
        "pro"            => ModuleTier.Pro,
        "advanced"       => ModuleTier.Advanced,
        "avanzado"       => ModuleTier.Advanced,
        _                => ModuleTier.Free,
    };

    /// <summary>Acepta Base64 estándar o "URL-safe" sin padding y lo normaliza.</summary>
    private static string PadBase64(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        var pad = s.Length % 4;
        return pad == 0 ? s : s + new string('=', 4 - pad);
    }

    private void SetTier(ModuleTier newTier)
    {
        ModuleTier old;
        lock (_stateLock)
        {
            old = _currentTier;
            _currentTier = newTier;
        }
        if (old != newTier)
            TierChanged?.Invoke(this, newTier);
    }
}
