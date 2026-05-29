using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace DexSuite.App.Services;

/// <summary>
/// Implementación de <see cref="ISettingsService"/> con persistencia a JSON
/// y debounce para coalesce de escrituras cuando el usuario tochea varios
/// toggles seguidos.
/// </summary>
public sealed class SettingsService : ISettingsService, IDisposable
{
    private readonly ILogger<SettingsService> _logger;
    private readonly string _path;
    private readonly SemaphoreSlim _ioLock = new(1, 1);

    // Estado del debounce: snapshot pendiente + timer que dispara el guardado.
    private readonly object _scheduleLock = new();
    private AppSettings? _pendingSnapshot;
    private CancellationTokenSource? _pendingCts;

    /// <summary>Ventana de debounce: si llegan más cambios, se reinicia.</summary>
    private static readonly TimeSpan DebounceWindow = TimeSpan.FromMilliseconds(400);

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public SettingsService(ILogger<SettingsService> logger)
    {
        _logger = logger;
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DexSuite");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_path)) return new AppSettings();
            var json = File.ReadAllText(_path);
            var parsed = JsonSerializer.Deserialize<AppSettings>(json);
            return parsed ?? new AppSettings();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "settings.json corrupto o ilegible; se usan valores por defecto");
            return new AppSettings();
        }
    }

    public void ScheduleSave(AppSettings settings)
    {
        CancellationToken token;
        lock (_scheduleLock)
        {
            _pendingSnapshot = settings;
            _pendingCts?.Cancel();
            _pendingCts = new CancellationTokenSource();
            token = _pendingCts.Token;
        }

        _ = DelayThenSaveAsync(token);
    }

    public async Task FlushAsync()
    {
        AppSettings? snap;
        lock (_scheduleLock)
        {
            _pendingCts?.Cancel();
            snap = _pendingSnapshot;
            _pendingSnapshot = null;
        }
        if (snap is not null) await WriteAsync(snap);
    }

    // ── implementación ───────────────────────────────────────────────────────

    private async Task DelayThenSaveAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(DebounceWindow, ct);
        }
        catch (TaskCanceledException) { return; }

        AppSettings? snap;
        lock (_scheduleLock)
        {
            // Otro ScheduleSave puede haber pisado el snapshot; cogemos el último.
            snap = _pendingSnapshot;
            _pendingSnapshot = null;
        }
        if (snap is not null) await WriteAsync(snap);
    }

    private async Task WriteAsync(AppSettings snap)
    {
        await _ioLock.WaitAsync();
        try
        {
            var json = JsonSerializer.Serialize(snap, JsonOpts);
            // Escritura atómica: temp + replace, para que un corte de luz no
            // deje el JSON a medio escribir.
            var tmp = _path + ".tmp";
            await File.WriteAllTextAsync(tmp, json);
            File.Move(tmp, _path, overwrite: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo persistir settings.json");
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public void Dispose()
    {
        _pendingCts?.Cancel();
        _pendingCts?.Dispose();
        _ioLock.Dispose();
    }
}
