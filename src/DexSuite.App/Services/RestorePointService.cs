using System.Management;
using Microsoft.Extensions.Logging;

namespace DexSuite.App.Services;

/// <summary>
/// Implementación de <see cref="IRestorePointService"/> mediante WMI.
///
/// La clase WMI <c>SystemRestore</c> vive en el namespace <c>root\default</c>.
/// El método <c>CreateRestorePoint</c> acepta:
///   - sDescription   : texto visible en "Restaurar sistema" del Panel de Control
///   - nRestorePointType: 12 = MODIFY_SETTINGS (adecuado para cambios de config)
///   - nEventType     : 100 = BEGIN_SYSTEM_CHANGE
///
/// Notas:
///   - Requiere admin (garantizado por app.manifest).
///   - Windows limita la frecuencia a 1 punto/24 h por defecto; si se llama
///     antes de que pase ese tiempo, WMI devuelve éxito pero no crea el punto.
///     Esto es comportamiento esperado del SO.
///   - Si Restauración del sistema está desactivada en C:, WMI devuelve error.
/// </summary>
public sealed class RestorePointService : IRestorePointService
{
    private readonly ILogger<RestorePointService> _logger;

    public RestorePointService(ILogger<RestorePointService> logger)
    {
        _logger = logger;
    }

    public Task<RestorePointResult> CreateAsync(string description, CancellationToken ct = default)
        => Task.Run(() => DoCreate(description), ct);

    public Task<bool> IsEnabledAsync()
        => Task.Run(CheckEnabled);

    // ── implementación privada ────────────────────────────────────────────────

    private RestorePointResult DoCreate(string description)
    {
        try
        {
            // Conexión a root\default donde vive la clase SystemRestore.
            var scope   = new ManagementScope(@"\\localhost\root\default");
            var mc      = new ManagementClass(scope,
                              new ManagementPath("SystemRestore"), null);

            var inParams = mc.GetMethodParameters("CreateRestorePoint");
            inParams["sDescription"]      = description;
            inParams["nRestorePointType"] = 12;  // MODIFY_SETTINGS
            inParams["nEventType"]        = 100; // BEGIN_SYSTEM_CHANGE

            var result = mc.InvokeMethod("CreateRestorePoint", inParams, null);
            var returnValue = Convert.ToInt32(result["ReturnValue"]);

            // 0 = éxito; el SO puede silenciosamente omitir el punto si ya se
            // creó uno en las últimas 24 h, pero WMI devuelve 0 igual.
            if (returnValue == 0)
            {
                _logger.LogInformation("Punto de restauración creado: '{Desc}'", description);
                return new RestorePointResult(true, description);
            }

            _logger.LogWarning("WMI CreateRestorePoint devolvió código {Code}", returnValue);
            return new RestorePointResult(false, $"WMI error {returnValue}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo crear el punto de restauración");
            return new RestorePointResult(false, ex.Message);
        }
    }

    private bool CheckEnabled()
    {
        try
        {
            // La tabla SystemRestoreConfig guarda si cada disco tiene SR activo.
            using var searcher = new ManagementObjectSearcher(
                @"root\default",
                "SELECT * FROM SystemRestoreConfig WHERE Drive='C:\\'");

            foreach (ManagementObject obj in searcher.Get())
            {
                // RPSessionInterval == 0 → desactivado
                var interval = Convert.ToInt32(obj["RPSessionInterval"]);
                return interval > 0;
            }
            // Sin filas = no hay configuración explícita → probablemente activo.
            return true;
        }
        catch
        {
            // No se puede consultar WMI → asumimos que está activo y dejamos que
            // CreateAsync falle con un mensaje de error concreto si no lo está.
            return true;
        }
    }
}
