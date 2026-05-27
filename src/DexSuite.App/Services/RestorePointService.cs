using System.Diagnostics;
using System.Management;
using Microsoft.Extensions.Logging;

namespace DexSuite.App.Services;

/// <summary>
/// Implementación de <see cref="IRestorePointService"/> mediante WMI.
///
/// La clase WMI <c>SystemRestore</c> vive en el namespace <c>root\default</c>.
/// El método <c>CreateRestorePoint</c> acepta:
///   - sDescription      : texto visible en "Restaurar sistema" del Panel de Control
///   - nRestorePointType : 12 = MODIFY_SETTINGS (adecuado para cambios de config)
///   - nEventType        : 100 = BEGIN_SYSTEM_CHANGE
///
/// Notas:
///   - Requiere admin (garantizado por app.manifest).
///   - Windows limita la frecuencia a 1 punto cada 1440 minutos (24 h) por
///     defecto vía SystemRestorePointCreationFrequency. Al primer arranque de
///     la app forzamos a 0 (sin throttling) en HKLM para que cada vez que el
///     usuario optimice se cree de verdad un punto.
///   - Si Restauración del sistema está desactivada en C:, WMI devuelve un
///     código 0x80042302 ("Not Found"). Lo manejamos llamando a
///     <c>Enable-ComputerRestore</c> vía PowerShell antes de reintentar.
/// </summary>
public sealed class RestorePointService : IRestorePointService
{
    private readonly ILogger<RestorePointService> _logger;

    // Evitamos llamar a Enable-ComputerRestore más de una vez por sesión —
    // es lento y requiere admin pero el SO lo recuerda entre llamadas.
    private bool _restoreEnableAttempted;

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
        // Quita el throttling de 1 punto/24 h. Idempotente, barato. Si falla,
        // lo registramos pero seguimos: el punto se intentará crear igual y
        // si el SO lo descarta por throttling lo veremos como returnValue == 0
        // pero sin crear nada físicamente.
        TryRemoveFrequencyThrottle();

        var first = TryCreate(description);
        if (first.Success) return first;

        // Si el primer intento falla por "Not Found" / SR desactivado en C:,
        // intentamos habilitar SystemRestore y reintentamos UNA vez.
        if (!_restoreEnableAttempted && LooksLikeRestoreDisabled(first.Message))
        {
            _restoreEnableAttempted = true;
            _logger.LogWarning("Restauración del sistema parece desactivada en C: — habilitando…");
            if (TryEnableSystemRestore())
            {
                // Brief pause so VSS/srservice has time to initialize after being started
                System.Threading.Thread.Sleep(800);
                var second = TryCreate(description);
                if (second.Success) return second;
                return new RestorePointResult(false,
                    $"Restauración del sistema habilitada, pero el segundo intento falló: {second.Message}");
            }
            return new RestorePointResult(false,
                "Restauración del sistema está desactivada en C: y no se pudo activar automáticamente. " +
                "Activa 'Protección del sistema' en C: desde Panel de control → Sistema → Configuración avanzada del sistema.");
        }

        return first;
    }

    /// <summary>Intenta crear el punto sin reintentar enable. Devuelve el resultado tal cual.</summary>
    private RestorePointResult TryCreate(string description)
    {
        try
        {
            var scope = new ManagementScope(@"\\localhost\root\default");
            var mc    = new ManagementClass(scope, new ManagementPath("SystemRestore"), null);

            var inParams = mc.GetMethodParameters("CreateRestorePoint");
            inParams["sDescription"]      = description;
            inParams["nRestorePointType"] = 12;  // MODIFY_SETTINGS
            inParams["nEventType"]        = 100; // BEGIN_SYSTEM_CHANGE

            var result      = mc.InvokeMethod("CreateRestorePoint", inParams, null);
            var returnValue = Convert.ToInt32(result["ReturnValue"]);

            if (returnValue == 0)
            {
                _logger.LogInformation("Punto de restauración creado: '{Desc}'", description);
                return new RestorePointResult(true, description);
            }
            _logger.LogWarning("WMI CreateRestorePoint devolvió código {Code}", returnValue);
            return new RestorePointResult(false, MapWmiError(returnValue));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Excepción WMI creando punto de restauración");
            return new RestorePointResult(false, ex.Message);
        }
    }

    /// <summary>
    /// Patrones que indican que System Restore está apagado en la unidad: la
    /// llamada WMI vuelve con "Not found" en español o inglés.
    /// </summary>
    private static bool LooksLikeRestoreDisabled(string message)
    {
        if (string.IsNullOrEmpty(message)) return false;
        var m = message.ToLowerInvariant();
        return m.Contains("not found") || m.Contains("no encontrad") ||
               m.Contains("0x80042302") || m.Contains("disabled");
    }

    /// <summary>
    /// Activa System Restore en C: vía PowerShell <c>Enable-ComputerRestore</c>.
    /// Requiere admin (ya garantizado por el manifest UAC). Devuelve true si
    /// el comando terminó con exit code 0.
    /// </summary>
    private bool TryEnableSystemRestore()
    {
        try
        {
            // Start VSS (Volume Shadow Copy) and srservice (System Restore Service) first,
            // then enable System Restore. -ErrorAction SilentlyContinue for the service starts
            // so the command doesn't fail if they're already running.
            var psi = new ProcessStartInfo
            {
                FileName               = "powershell.exe",
                Arguments              = "-NoProfile -ExecutionPolicy Bypass -Command \"" +
                                         "Start-Service VSS -ErrorAction SilentlyContinue; " +
                                         "Start-Service srservice -ErrorAction SilentlyContinue; " +
                                         "Enable-ComputerRestore -Drive 'C:\\' -ErrorAction Stop\"",
                CreateNoWindow         = true,
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
            };
            using var p = Process.Start(psi);
            if (p is null) return false;
            // 30 s es más que suficiente — Enable-ComputerRestore es casi instantáneo
            // cuando el SO ya tiene la feature disponible.
            if (!p.WaitForExit(30_000))
            {
                try { p.Kill(); } catch { /* nothing to do */ }
                return false;
            }
            return p.ExitCode == 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Enable-ComputerRestore falló");
            return false;
        }
    }

    /// <summary>
    /// Pone <c>SystemRestorePointCreationFrequency = 0</c> en HKLM para que el SO
    /// no descarte llamadas a CreateRestorePoint que estén dentro de las
    /// últimas 24 h del último punto creado. Idempotente.
    /// </summary>
    private void TryRemoveFrequencyThrottle()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore");
            key?.SetValue("SystemRestorePointCreationFrequency", 0, Microsoft.Win32.RegistryValueKind.DWord);
        }
        catch (Exception ex)
        {
            // No es bloqueante — el throttling solo afecta si han pasado < 24h.
            _logger.LogWarning(ex, "No se pudo quitar SystemRestorePointCreationFrequency");
        }
    }

    /// <summary>
    /// Traduce códigos de error WMI a mensajes con cierto contexto.
    /// </summary>
    private static string MapWmiError(int code) => code switch
    {
        unchecked((int)0x80042302) => "Restauración del sistema no encontrada (deshabilitada en C:).",
        unchecked((int)0x80070005) => "Acceso denegado: la app debe ejecutarse como administrador.",
        unchecked((int)0x80070422) => "El servicio 'Volume Shadow Copy' (VSS) está deshabilitado.",
        _ => $"WMI error 0x{code:X8}",
    };

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
