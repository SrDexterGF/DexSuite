using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using DexSuite.App.Models;

namespace DexSuite.App.Services.CleanupModules;

/// <summary>
/// M12 — Winget upgrade --all.
/// Reutiliza el <see cref="IWingetService"/> ya existente (que llama a winget.exe
/// directamente sin .bat). Emite cada línea de stdout como Info y respeta cancelación.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class M12WingetUpgrade : ModuleExecutorBase
{
    public override int ModuleId => 12;

    private readonly IWingetService _winget;

    public M12WingetUpgrade(IWingetService winget) => _winget = winget;

    public override async IAsyncEnumerable<ModuleProgress> ExecuteAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        yield return Header("Actualizando todas las apps");

        if (!_winget.IsAvailable)
        {
            yield return Warn("winget.exe no está disponible en este equipo. App Installer no instalado.");
            yield return Done("M12 omitido");
            yield break;
        }

        yield return Step("Actualizando todas las aplicaciones con Winget");
        yield return Info("Esto puede tardar si hay muchas apps o actualizaciones grandes.");

        // El servicio expone IProgress<string>; lo convertimos a stream cancelable.
        var queue = System.Threading.Channels.Channel.CreateUnbounded<string>(
            new System.Threading.Channels.UnboundedChannelOptions { SingleReader = true });

        var progress = new Progress<string>(line => queue.Writer.TryWrite(line));

        var upgradeTask = Task.Run(async () =>
        {
            try
            {
                var result = await _winget.UpgradeAllAsync(progress, ct).ConfigureAwait(false);
                queue.Writer.TryWrite($"__RESULT__ {result.PackagesUpdated} {result.Succeeded}");
            }
            catch (Exception ex)
            {
                queue.Writer.TryWrite($"__ERROR__ {ex.Message}");
            }
            finally
            {
                queue.Writer.TryComplete();
            }
        }, ct);

        int packagesUpdated = 0;
        bool succeeded = false;
        string? error = null;

        while (await queue.Reader.WaitToReadAsync(ct).ConfigureAwait(false))
        {
            while (queue.Reader.TryRead(out var line))
            {
                if (line.StartsWith("__RESULT__"))
                {
                    var parts = line.Split(' ');
                    int.TryParse(parts[1], out packagesUpdated);
                    succeeded = bool.TryParse(parts[2], out var s) && s;
                }
                else if (line.StartsWith("__ERROR__"))
                {
                    error = line.Substring("__ERROR__ ".Length);
                }
                else if (!string.IsNullOrWhiteSpace(line) && !IsWingetNoiseLine(line))
                {
                    yield return Info(line);
                }
            }
        }

        await upgradeTask.ConfigureAwait(false);

        if (error is not null)
        {
            yield return Err($"Winget falló: {error}");
        }
        else
        {
            yield return succeeded
                ? Ok($"Winget completado ({packagesUpdated} paquete(s) actualizado(s))")
                : Warn("Winget terminó con código distinto de 0");
        }

        yield return Done("M12 completado");
    }

    /// <summary>
    /// Returns true for lines that are winget UI noise (spinner animation chars,
    /// Unicode progress bars). These lines pollute the log without adding value.
    /// </summary>
    private static bool IsWingetNoiseLine(string line)
    {
        var t = line.Trim();
        if (t.Length == 0) return false;

        // Spinner chars only: |, /, -, \
        if (t.Length <= 2 && t.All(c => c is '|' or '/' or '-' or '\\'))
            return true;

        // Unicode block-element progress bars (█ ▒ ░) — winget download progress
        if (t.IndexOfAny(['█', '▒', '░']) >= 0)
            return true;

        return false;
    }
}
