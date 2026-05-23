using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;

namespace DexSuite.App.Services;

/// <summary>
/// Lanza DexSuite_CleanUp_v*.bat como subproceso heredando la elevacion
/// administrativa del proceso anfitrion (gracias al app.manifest con
/// requireAdministrator). Asi el .bat NO se relanza por UAC y conservamos
/// stdout/stderr redirigidos.
///
/// Conduce el menu del .bat por stdin con tres lineas:
///   1. "M"        seleccion manual
///   2. "1 3 7"    ids de modulos elegidos, separados por espacios
///   3. "S"        al terminar, salir del menu "Volver al menu principal?"
///
/// Ademas emite un "heartbeat" cada <see cref="HeartbeatSeconds"/> segundos
/// para que la UI muestre que sigue trabajando aunque el .bat este en una
/// operacion larga sin stdout (SFC, DISM, etc.).
/// </summary>
public sealed class BatRunner : IBatRunner
{
    private const int HeartbeatSeconds = 5;

    public async IAsyncEnumerable<string> RunAsync(
        string batPath,
        IReadOnlyList<int> selectedModuleIds,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (!File.Exists(batPath))
            throw new FileNotFoundException("No se encontró el .bat de DexSuite", batPath);
        if (selectedModuleIds.Count == 0)
            yield break;

        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            // Doble comilla por los espacios en "Claude Environment W11" y "DexSuite (Script)".
            Arguments = $"/c \"\"{batPath}\"\"",
            WorkingDirectory = Path.GetDirectoryName(batPath)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.GetEncoding(1252),
            StandardErrorEncoding = Encoding.GetEncoding(1252),
        };

        var channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

        // Marca de tiempo del ultimo dato recibido del .bat. La usa el heartbeat
        // para decidir si emitir "sigue trabajando..." o callarse.
        var lastOutputUtc = DateTime.UtcNow;
        var startedUtc = DateTime.UtcNow;

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            lastOutputUtc = DateTime.UtcNow;
            channel.Writer.TryWrite(StripAnsi(e.Data));
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            lastOutputUtc = DateTime.UtcNow;
            channel.Writer.TryWrite("[ERR] " + StripAnsi(e.Data));
        };
        process.Exited += (_, _) => channel.Writer.TryComplete();

        if (!process.Start())
            throw new InvalidOperationException("No se pudo lanzar cmd.exe");

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // CTS interno para parar el heartbeat cuando el proceso termina o se cancela.
        using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var heartbeatTask = Task.Run(async () =>
        {
            try
            {
                while (!heartbeatCts.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(HeartbeatSeconds), heartbeatCts.Token);
                    var silenceSec = (DateTime.UtcNow - lastOutputUtc).TotalSeconds;
                    if (silenceSec >= HeartbeatSeconds)
                    {
                        var elapsed = DateTime.UtcNow - startedUtc;
                        channel.Writer.TryWrite(
                            $"[...] {elapsed:mm\\:ss} - sigue trabajando (sin salida visible)");
                    }
                }
            }
            catch (OperationCanceledException) { /* esperado al terminar */ }
        }, heartbeatCts.Token);

        // Damos al .bat un instante para pintar su menu inicial.
        // Sin esto, set /p puede consumir nuestras respuestas antes de mostrarlo.
        try
        {
            await Task.Delay(500, ct);
            await process.StandardInput.WriteLineAsync("M".AsMemory(), ct);
            await process.StandardInput.WriteLineAsync(string.Join(" ", selectedModuleIds).AsMemory(), ct);
            // Al final el .bat pregunta Enter/S; pedimos salir.
            await process.StandardInput.WriteLineAsync("S".AsMemory(), ct);
            await process.StandardInput.FlushAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            channel.Writer.TryWrite($"[ERR] No se pudo escribir al subproceso: {ex.Message}");
        }
        finally
        {
            try { process.StandardInput.Close(); } catch { /* ya cerrado */ }
        }

        // Reenviamos stdout/stderr al consumidor hasta que el proceso termine
        // o el usuario cancele. Si cancela, matamos el arbol de procesos.
        var cancelled = false;
        try
        {
            while (await channel.Reader.WaitToReadAsync(ct))
            {
                while (channel.Reader.TryRead(out var line))
                    yield return line;
            }
        }
        finally
        {
            cancelled = ct.IsCancellationRequested;
            heartbeatCts.Cancel();
            try { await heartbeatTask; } catch { /* swallowed */ }

            if (!process.HasExited)
            {
                try { process.Kill(entireProcessTree: true); } catch { /* nothing to do */ }
            }
        }

        if (cancelled)
            yield return "[!] Ejecucion cancelada por el usuario.";
    }

    /// <summary>
    /// Quita codigos ANSI tipo ESC[35;40m que el .bat emite para pintarse en magenta.
    /// </summary>
    private static string StripAnsi(string line)
    {
        if (string.IsNullOrEmpty(line) || !line.Contains(''))
            return line;

        var sb = new StringBuilder(line.Length);
        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == '' && i + 1 < line.Length && line[i + 1] == '[')
            {
                i += 2;
                while (i < line.Length && !char.IsLetter(line[i])) i++;
                continue;
            }
            sb.Append(line[i]);
        }
        return sb.ToString();
    }
}
