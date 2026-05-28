using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using DexSuite.App.Models;

namespace DexSuite.App.Services.CleanupModules;

/// <summary>
/// M10 — SFC + DISM RestoreHealth.
/// Verificación y reparación del sistema vía Process streaming.
/// Reutiliza el mismo patrón que <see cref="SecurityCheckService"/>.
/// Migrado del bloque RUN_10 del .bat.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class M10SfcDism : ModuleExecutorBase
{
    public override int ModuleId => 10;

    public override async IAsyncEnumerable<ModuleProgress> ExecuteAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        yield return Header("Verificación y Reparación del Sistema");
        yield return Info("Aviso: SFC y DISM pueden tardar varios minutos.");

        var sfc  = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "sfc.exe");
        var dism = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "Dism.exe");

        // SFC /scannow.
        yield return Step("SFC - Escaneando y reparando archivos del sistema");
        if (File.Exists(sfc))
        {
            await foreach (var line in StreamProcessAsync(sfc, "/scannow", ct: ct))
            {
                if (ct.IsCancellationRequested) yield break;
                if (!string.IsNullOrWhiteSpace(line))
                    yield return Info(line);
            }
            yield return Ok("SFC completado");
        }
        else yield return Warn("sfc.exe no encontrado");

        // DISM /Online /Cleanup-Image /RestoreHealth.
        if (ct.IsCancellationRequested) yield break;
        yield return Step("DISM - Reparando la imagen de Windows");
        if (File.Exists(dism))
        {
            await foreach (var line in StreamProcessAsync(
                dism, "/Online /Cleanup-Image /RestoreHealth", ct: ct))
            {
                if (ct.IsCancellationRequested) yield break;
                if (!string.IsNullOrWhiteSpace(line))
                    yield return Info(line);
            }
            yield return Ok("DISM completado");
        }
        else yield return Warn("Dism.exe no encontrado");

        yield return Done("M10 completado");
    }
}
