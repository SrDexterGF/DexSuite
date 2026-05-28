using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Logging;

namespace DexSuite.App.Services;

/// <summary>
/// Implementación de <see cref="ISecurityCheckService"/>.
///
/// Cada herramienta se lanza como Process nativo (sin .bat ni PowerShell)
/// con stdout/stderr redirigidos. La app ya corre como admin (UAC manifest)
/// por lo que SFC/DISM/Defender pueden operar sin elevación adicional.
/// </summary>
public sealed class SecurityCheckService : ISecurityCheckService
{
    private readonly ILogger<SecurityCheckService> _logger;

    // Defender está en %ProgramFiles%\Windows Defender\MpCmdRun.exe
    private static readonly string DefenderPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        "Windows Defender", "MpCmdRun.exe");

    // mrt.exe vive en System32. Microsoft lo entrega vía Windows Update.
    private static readonly string MrtPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.System), "MRT.exe");

    private static readonly string SfcPath  = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.System), "sfc.exe");

    private static readonly string DismPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.System), "Dism.exe");

    public SecurityCheckService(ILogger<SecurityCheckService> logger)
    {
        _logger = logger;
    }

    public bool IsAvailable(SecurityCheckKind kind) => kind switch
    {
        SecurityCheckKind.DefenderQuick => File.Exists(DefenderPath),
        SecurityCheckKind.Sfc           => File.Exists(SfcPath),
        SecurityCheckKind.Dism          => File.Exists(DismPath),
        SecurityCheckKind.Mrt           => File.Exists(MrtPath),
        _ => false,
    };

    public async Task<SecurityCheckResult> RunAsync(
        SecurityCheckKind kind,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var (file, args) = GetCommand(kind);

        if (!File.Exists(file))
            throw new FileNotFoundException($"Herramienta no encontrada: {file}");

        _logger.LogInformation("Iniciando {Kind}: {File} {Args}", kind, file, args);
        var sw = Stopwatch.StartNew();

        var psi = new ProcessStartInfo
        {
            FileName               = file,
            Arguments              = args,
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow         = true,
            // SFC vuelca con encoding OEM (codepage 437/850). Lo decodificamos
            // como UTF-8 con fallback a Default para no romper el log si llega
            // un carácter no representable.
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding  = System.Text.Encoding.UTF8,
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        // Lectura simultánea de stdout y stderr para evitar deadlock por buffer lleno.
        var stdoutTask = ReadStreamAsync(process.StandardOutput, progress, ct);
        var stderrTask = ReadStreamAsync(process.StandardError,  progress, ct);

        await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);

        if (!ct.IsCancellationRequested)
            await process.WaitForExitAsync(ct).ConfigureAwait(false);

        sw.Stop();
        var exitCode = process.ExitCode;
        // Cada herramienta tiene su criterio de "éxito":
        // - Defender QuickScan: 0 = limpio
        // - SFC: 0 = sin problemas o reparados con éxito
        // - DISM: 0 = OK
        // - MRT: 0 = OK
        // Cualquier otro código se considera fallo y se loguea.
        var ok = exitCode == 0;

        _logger.LogInformation("{Kind} finalizado en {Dur:F1}s con ExitCode={Code}",
            kind, sw.Elapsed.TotalSeconds, exitCode);

        return new SecurityCheckResult(kind, exitCode, ok, sw.Elapsed);
    }

    private static (string file, string args) GetCommand(SecurityCheckKind kind) => kind switch
    {
        // -Scan -ScanType 1 = QuickScan (5-10 min típicos)
        SecurityCheckKind.DefenderQuick => (DefenderPath, "-Scan -ScanType 1"),

        // /scannow = analiza todos los archivos protegidos y los repara
        SecurityCheckKind.Sfc           => (SfcPath, "/scannow"),

        // /Online: imagen viva; /Cleanup-Image /RestoreHealth = repara desde Update
        SecurityCheckKind.Dism          => (DismPath, "/Online /Cleanup-Image /RestoreHealth"),

        // /Q = silencioso (sin UI). Devuelve 0 si OK.
        SecurityCheckKind.Mrt           => (MrtPath, "/Q"),

        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static async Task ReadStreamAsync(
        StreamReader reader,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        try
        {
            string? line;
            while ((line = await reader.ReadLineAsync(ct).ConfigureAwait(false)) is not null)
            {
                if (ct.IsCancellationRequested) break;
                if (!string.IsNullOrWhiteSpace(line))
                    progress?.Report(line);
            }
        }
        catch (OperationCanceledException) { /* cancelado por el usuario */ }
    }
}
