using System.Diagnostics;
using System.Management;
using Microsoft.Extensions.Logging;

namespace DexSuite.App.Services;

/// <summary>
/// Implementación de <see cref="IRestorePointService"/> con tres niveles de fallback:
///   1. WMI SystemRestore.CreateRestorePoint
///   2. PowerShell Checkpoint-Computer (más robusto en equipos donde WMI falla)
///   3. Enable-ComputerRestore + reintento si SR parece desactivado
///
/// Notas:
///   - Requiere admin (garantizado por app.manifest).
///   - Quita el throttling de 24 h via registro antes de cada intento.
/// </summary>
public sealed class RestorePointService : IRestorePointService
{
    private readonly ILogger<RestorePointService> _logger;

    // Evitamos llamar a Enable-ComputerRestore más de una vez por sesión.
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
        // Quita el throttling de 1 punto/24 h. Idempotente. Si falla, seguimos.
        TryRemoveFrequencyThrottle();

        // Intento 1: WMI (método preferido)
        var wmiResult = TryCreate(description);
        if (wmiResult.Success) return wmiResult;

        _logger.LogWarning("WMI CreateRestorePoint falló ({Msg}). Intentando Checkpoint-Computer…", wmiResult.Message);

        // Intento 2: Checkpoint-Computer (PowerShell — más robusto que WMI en algunos equipos)
        var psResult = TryCheckpointComputer(description);
        if (psResult.Success) return psResult;

        _logger.LogWarning("Checkpoint-Computer también falló ({Msg}).", psResult.Message);

        // Intento 3: Si parece que SR está desactivado, habilitarlo y reintentar
        if (!_restoreEnableAttempted &&
            (LooksLikeRestoreDisabled(wmiResult.Message) || LooksLikeRestoreDisabled(psResult.Message)))
        {
            _restoreEnableAttempted = true;
            _logger.LogWarning("Restauración del sistema parece desactivada en C: — habilitando…");
            if (TryEnableSystemRestore())
            {
                // Pause so VSS/srservice has time to initialize
                System.Threading.Thread.Sleep(1200);
                var retry = TryCheckpointComputer(description);
                if (retry.Success) return retry;
                return new RestorePointResult(false,
                    $"Restauración del sistema habilitada, pero el reintento falló: {retry.Message}");
            }
            return new RestorePointResult(false,
                "Restauración del sistema está desactivada en C: y no se pudo activar automáticamente. " +
                "Actívala desde: Panel de control → Sistema → Configuración avanzada del sistema → Protección del sistema.");
        }

        return new RestorePointResult(false,
            $"No se pudo crear el punto de restauración. WMI: {wmiResult.Message}. PowerShell: {psResult.Message}");
    }

    /// <summary>Intenta crear el punto via WMI. Devuelve el resultado tal cual.</summary>
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
                _logger.LogInformation("Punto de restauración creado via WMI: '{Desc}'", description);
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
    /// Crea el punto de restauración usando PowerShell <c>Checkpoint-Computer</c>.
    /// Más fiable que WMI en equipos donde el proveedor WMI SystemRestore no está
    /// bien registrado. Requiere admin.
    /// </summary>
    private RestorePointResult TryCheckpointComputer(string description)
    {
        try
        {
            // Sanitize description: PowerShell injection guard (quitar comillas simples)
            var safeDesc = description.Replace("'", "");

            var psi = new ProcessStartInfo
            {
                FileName               = "powershell.exe",
                Arguments              = $"-NoProfile -ExecutionPolicy Bypass -Command " +
                                         $"\"Checkpoint-Computer -Description '{safeDesc}' " +
                                         $"-RestorePointType MODIFY_SETTINGS -ErrorAction Stop\"",
                CreateNoWindow         = true,
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
            };
            using var p = Process.Start(psi);
            if (p is null) return new RestorePointResult(false, "No se pudo iniciar powershell.exe");

            var stderr = p.StandardError.ReadToEnd();
            p.WaitForExit(60_000); // 60 s timeout — Checkpoint-Computer puede tardar

            if (p.ExitCode == 0)
            {
                _logger.LogInformation("Punto de restauración creado via Checkpoint-Computer: '{Desc}'", description);
                return new RestorePointResult(true, description);
            }

            var errMsg = string.IsNullOrWhiteSpace(stderr) ? $"exit code {p.ExitCode}" : stderr.Trim();
            return new RestorePointResult(false, errMsg);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Checkpoint-Computer falló");
            return new RestorePointResult(false, ex.Message);
        }
    }

    /// <summary>
    /// Patrones que indican que System Restore está apagado en la unidad.
    /// </summary>
    private static bool LooksLikeRestoreDisabled(string? message)
    {
        if (string.IsNullOrEmpty(message)) return false;
        var m = message.ToLowerInvariant();
        return m.Contains("not found")        || m.Contains("no encontrad")    ||
               m.Contains("0x80042302")       || m.Contains("disabled")        ||
               m.Contains("deshabilitado")    || m.Contains("serviceDisabled") ||
               m.Contains("system restore") && m.Contains("not enabled")       ||
               m.Contains("restore is turn");
    }

    /// <summary>
    /// Activa System Restore en C: vía PowerShell <c>Enable-ComputerRestore</c>.
    /// </summary>
    private bool TryEnableSystemRestore()
    {
        try
        {
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
            // Forzamos startup type a Manual antes de intentar arrancar.
            // Sin este paso, Start-Service falla si el servicio está marcado Disabled.
            psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"" +
                            "Set-Service VSS      -StartupType Manual -ErrorAction SilentlyContinue; " +
                            "Set-Service srservice -StartupType Manual -ErrorAction SilentlyContinue; " +
                            "Start-Service VSS      -ErrorAction SilentlyContinue; " +
                            "Start-Service srservice -ErrorAction SilentlyContinue; " +
                            "Enable-ComputerRestore -Drive 'C:\\' -ErrorAction Stop\"";
            using var p = Process.Start(psi);
            if (p is null) return false;
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
    /// Pone <c>SystemRestorePointCreationFrequency = 0</c> en HKLM para eliminar
    /// el throttling de 24 h. Idempotente.
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
            _logger.LogWarning(ex, "No se pudo quitar SystemRestorePointCreationFrequency");
        }
    }

    /// <summary>
    /// Traduce códigos de error WMI a mensajes con contexto.
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
            using var searcher = new ManagementObjectSearcher(
                @"root\default",
                "SELECT * FROM SystemRestoreConfig WHERE Drive='C:\\'");

            foreach (ManagementObject obj in searcher.Get())
            {
                // RPSessionInterval == 0 → desactivado
                var interval = Convert.ToInt32(obj["RPSessionInterval"]);
                return interval > 0;
            }
            return true;
        }
        catch
        {
            return true;
        }
    }
}
