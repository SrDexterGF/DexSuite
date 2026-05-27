namespace DexSuite.App.Services;

/// <summary>Tipo de comprobación de seguridad/integridad disponible.</summary>
public enum SecurityCheckKind
{
    /// <summary>Escaneo rápido de Windows Defender (MpCmdRun -ScanType 1).</summary>
    DefenderQuick = 0,

    /// <summary>System File Checker (sfc /scannow). Repara archivos de sistema dañados.</summary>
    Sfc = 1,

    /// <summary>DISM /RestoreHealth. Repara la imagen de Windows desde WinSxS o WU.</summary>
    Dism = 2,

    /// <summary>Microsoft Malicious Software Removal Tool (mrt /Q).</summary>
    Mrt = 3,
}

/// <summary>Resultado de una ejecución individual.</summary>
public record SecurityCheckResult(SecurityCheckKind Kind, int ExitCode, bool Succeeded, TimeSpan Duration);

/// <summary>
/// Ejecuta herramientas nativas de Windows para comprobaciones de seguridad
/// e integridad del sistema. Todas se invocan como procesos elevados sin
/// dependencias externas; la salida se reporta vía <see cref="IProgress{T}"/>
/// para que el ViewModel pueda volcarla al log interno en tiempo real.
/// </summary>
public interface ISecurityCheckService
{
    /// <summary>
    /// Ejecuta una herramienta y reporta su stdout/stderr línea a línea.
    /// </summary>
    Task<SecurityCheckResult> RunAsync(
        SecurityCheckKind kind,
        IProgress<string>? progress = null,
        CancellationToken ct = default);

    /// <summary>True si la herramienta está disponible en el sistema.</summary>
    bool IsAvailable(SecurityCheckKind kind);
}
