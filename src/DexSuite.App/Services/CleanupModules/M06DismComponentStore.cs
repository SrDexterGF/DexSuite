using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using DexSuite.App.Models;

namespace DexSuite.App.Services.CleanupModules;

/// <summary>
/// M6 — DISM Component Store.
/// AnalyzeComponentStore + StartComponentCleanup vía Process streaming.
/// DISM no tiene API .NET razonable, mantener exe directo es lo estándar.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class M06DismComponentStore : ModuleExecutorBase
{
    public override int ModuleId => 6;

    public override async IAsyncEnumerable<ModuleProgress> ExecuteAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        yield return Header("DISM - Component Store");
        yield return Info("Este módulo puede tardar entre 5 y 20 minutos, es normal.");

        var dism = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System), "Dism.exe");

        if (!File.Exists(dism))
        {
            yield return Err("Dism.exe no encontrado en System32");
            yield return Done("M6 abortado");
            yield break;
        }

        yield return Step("Analizando el Component Store");
        await foreach (var line in StreamProcessAsync(
            dism, "/Online /Cleanup-Image /AnalyzeComponentStore", ct: ct))
        {
            if (ct.IsCancellationRequested) yield break;
            if (!string.IsNullOrWhiteSpace(line))
                yield return Info(line);
        }
        yield return Ok("Análisis completado");

        if (ct.IsCancellationRequested) yield break;
        yield return Step("Eliminando componentes obsoletos");
        await foreach (var line in StreamProcessAsync(
            dism, "/Online /Cleanup-Image /StartComponentCleanup", ct: ct))
        {
            if (ct.IsCancellationRequested) yield break;
            if (!string.IsNullOrWhiteSpace(line))
                yield return Info(line);
        }
        yield return Ok("Limpieza del Component Store completada");

        yield return Done("M6 completado");
    }
}
