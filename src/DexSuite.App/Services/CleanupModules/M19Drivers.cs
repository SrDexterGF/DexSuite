using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Text;
using DexSuite.App.Models;

namespace DexSuite.App.Services.CleanupModules;

/// <summary>
/// M19 — Drivers.
/// pnputil /scan-devices + /enum-drivers (contando entradas OEM), arranca
/// wuauserv y lanza UsoClient ScanInstallWait para que Windows Update busque
/// drivers. Migrado del bloque RUN_19 / :mod_drivers del .bat.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class M19Drivers : ModuleExecutorBase
{
    public override int ModuleId => 19;

    public override async IAsyncEnumerable<ModuleProgress> ExecuteAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        yield return Header("Drivers - Verificación y actualización");

        var system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var pnputil  = Path.Combine(system32, "pnputil.exe");
        var usoclient = Path.Combine(system32, "UsoClient.exe");

        // ── pnputil /scan-devices ─────────────────────────────────────
        yield return Step("Refrescando catálogo de hardware (pnputil /scan-devices)");
        if (File.Exists(pnputil))
        {
            await RunProcessAsync(pnputil, "/scan-devices", ct);
            yield return Ok("Catálogo de hardware refrescado");
        }
        else yield return Warn("pnputil.exe no encontrado");

        // ── pnputil /enum-drivers — contamos OEM*.inf ─────────────────
        if (ct.IsCancellationRequested) yield break;
        yield return Step("Listando drivers OEM instalados");
        int oemCount = 0;
        if (File.Exists(pnputil))
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName               = pnputil,
                    Arguments              = "/enum-drivers",
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow         = true,
                    StandardOutputEncoding = Encoding.UTF8,
                };
                using var p = Process.Start(psi);
                if (p is not null)
                {
                    string? line;
                    while ((line = await p.StandardOutput.ReadLineAsync(ct).ConfigureAwait(false)) is not null)
                    {
                        if (line.Contains("oem", StringComparison.OrdinalIgnoreCase) &&
                            line.Contains(".inf", StringComparison.OrdinalIgnoreCase))
                            oemCount++;
                    }
                    await p.WaitForExitAsync(ct).ConfigureAwait(false);
                }
            }
            catch { /* ignora */ }
        }
        yield return Info($"Total entradas oem*.inf detectadas: {oemCount}");
        yield return Ok("Resumen mostrado");

        // ── Windows Update busca drivers ──────────────────────────────
        if (ct.IsCancellationRequested) yield break;
        yield return Step("Lanzando Windows Update para drivers (puede tardar varios minutos)");
        StartService("wuauserv");
        if (File.Exists(usoclient))
            await RunProcessAsync(usoclient, "ScanInstallWait", ct);
        yield return Ok("Windows Update lanzado para drivers");

        // ── Recordatorio ──────────────────────────────────────────────
        yield return Step("Recordatorio sobre drivers de fabricante");
        yield return Info("Algunos drivers requieren instalador oficial del fabricante:");
        yield return Info("  - NVIDIA  : nvidia.com/Download");
        yield return Info("  - AMD     : amd.com/support");
        yield return Info("  - Intel   : intel.com/content/www/us/en/download-center");
        yield return Ok("Recordatorio mostrado");

        yield return Done("M19 completado");
    }
}
