using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using DexSuite.App.Models;

namespace DexSuite.App.Services.CleanupModules;

/// <summary>
/// M8 — Red (DNS y gpupdate).
/// flushdns vía P/Invoke DnsFlushResolverCache, registerdns y gpupdate /force.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class M08NetworkReset : ModuleExecutorBase
{
    public override int ModuleId => 8;

    [DllImport("dnsapi.dll", EntryPoint = "DnsFlushResolverCache")]
    private static extern uint DnsFlushResolverCache();

    public override async IAsyncEnumerable<ModuleProgress> ExecuteAsync(
        IReadOnlySet<string>? enabledSubOps,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        yield return Header("Red y Directivas de Grupo");

        if (Want(enabledSubOps, "M08_dns_flush"))
        {
            yield return Step("Vaciando cache DNS");
            bool dnsOk = false;
            try { dnsOk = DnsFlushResolverCache() != 0; }
            catch { /* dnsapi.dll falló */ }

            if (dnsOk)
                yield return Ok("Cache DNS vaciada");
            else
            {
                // Fallback a ipconfig si la P/Invoke devuelve 0.
                var rc = await RunProcessAsync(
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "ipconfig.exe"),
                    "/flushdns", ct);
                yield return rc == 0 ? Ok("Cache DNS vaciada (fallback ipconfig)") : Warn("No se pudo vaciar la cache DNS");
            }
        }

        if (ct.IsCancellationRequested) yield break;
        if (Want(enabledSubOps, "M08_dns_register"))
        {
            yield return Step("Registrando equipo en DNS");
            var ipconfig = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "ipconfig.exe");
            var rcReg = await RunProcessAsync(ipconfig, "/registerdns", ct);
            yield return rcReg == 0 ? Ok("DNS registrado") : Warn($"ipconfig /registerdns ExitCode={rcReg}");
        }

        if (ct.IsCancellationRequested) yield break;
        if (Want(enabledSubOps, "M08_gpupdate"))
        {
            yield return Step("Forzando actualización de directivas de grupo");
            var gpupdate = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "gpupdate.exe");
            if (File.Exists(gpupdate))
            {
                await foreach (var line in StreamProcessAsync(gpupdate, "/force", ct: ct))
                {
                    if (ct.IsCancellationRequested) yield break;
                    if (!string.IsNullOrWhiteSpace(line))
                        yield return Info(line);
                }
                yield return Ok("Directivas de grupo actualizadas");
            }
            else
            {
                yield return Warn("gpupdate.exe no encontrado");
            }
        }

        yield return Done("M8 completado");
    }
}
