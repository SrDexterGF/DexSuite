using System.IO;
using System.Text;
using DexSuite.App.Data;
using DexSuite.App.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DexSuite.App.Services;

/// <summary>
/// Implementación de <see cref="IAppLogService"/> sobre EF Core SQLite.
///
/// - Cada llamada usa un <see cref="IDbContextFactory{TContext}"/> para evitar
///   compartir DbContext entre hilos.
/// - Las escrituras se serializan con un <see cref="SemaphoreSlim"/> para evitar
///   "database is locked" en SQLite cuando varios eventos llegan a la vez.
/// </summary>
public sealed class AppLogService : IAppLogService
{
    private readonly IDbContextFactory<DexSuiteDbContext> _factory;
    private readonly ILogger<AppLogService> _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public event EventHandler<LogEntry>? EntryAdded;

    public AppLogService(IDbContextFactory<DexSuiteDbContext> factory, ILogger<AppLogService> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public async Task WriteAsync(AppLogLevel level, AppLogCategory category, string message, string? details = null)
    {
        var entry = new LogEntry
        {
            TimestampUtc = DateTime.UtcNow,
            Level        = level,
            Category     = category,
            Message      = string.IsNullOrWhiteSpace(message) ? "(empty)" : Truncate(Sanitize(message), 500),
            Details      = string.IsNullOrWhiteSpace(details) ? null : details,
        };

        await _writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await using var db = await _factory.CreateDbContextAsync().ConfigureAwait(false);
            db.Logs.Add(entry);
            await db.SaveChangesAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo escribir la entrada de historial");
            return;
        }
        finally
        {
            _writeLock.Release();
        }

        try { EntryAdded?.Invoke(this, entry); }
        catch (Exception ex) { _logger.LogWarning(ex, "EntryAdded handler lanzó"); }
    }

    public Task InfoAsync   (AppLogCategory category, string message, string? details = null) => WriteAsync(AppLogLevel.Info,    category, message, details);
    public Task SuccessAsync(AppLogCategory category, string message, string? details = null) => WriteAsync(AppLogLevel.Success, category, message, details);
    public Task WarningAsync(AppLogCategory category, string message, string? details = null) => WriteAsync(AppLogLevel.Warning, category, message, details);
    public Task ErrorAsync  (AppLogCategory category, string message, string? details = null) => WriteAsync(AppLogLevel.Error,   category, message, details);

    public async Task<IReadOnlyList<LogEntry>> GetRecentAsync(int max = 500)
    {
        if (max <= 0) return Array.Empty<LogEntry>();

        await using var db = await _factory.CreateDbContextAsync().ConfigureAwait(false);
        return await db.Logs
            .AsNoTracking()
            .OrderByDescending(e => e.TimestampUtc)
            .Take(max)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    public async Task<int> ClearAllAsync()
    {
        await _writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await using var db = await _factory.CreateDbContextAsync().ConfigureAwait(false);
            // ExecuteDeleteAsync (EF Core 7+) evita cargar entidades a memoria.
            return await db.Logs.ExecuteDeleteAsync().ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<string> ExportToTextAsync(string targetPath)
    {
        var entries = await GetRecentAsync(int.MaxValue).ConfigureAwait(false);
        var finalPath = ResolveNonClashingPath(targetPath);

        Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);

        var sb = new StringBuilder(capacity: 4096);
        sb.AppendLine("DexSuite — Historial interno");
        sb.AppendLine($"Exportado: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Entradas: {entries.Count}");
        sb.AppendLine(new string('=', 60));

        // Más antiguas primero al exportar, leyendo en orden cronológico.
        foreach (var e in entries.OrderBy(e => e.TimestampUtc))
        {
            sb.AppendLine($"[{e.TimestampUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}] [{e.Level}] [{e.Category}] {e.Message}");
            if (!string.IsNullOrWhiteSpace(e.Details))
                sb.AppendLine($"    {e.Details.Replace("\n", "\n    ")}");
        }

        await File.WriteAllTextAsync(finalPath, sb.ToString(), Encoding.UTF8).ConfigureAwait(false);
        return finalPath;
    }

    // -------------------- helpers --------------------

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : s.Substring(0, max - 1) + "…";

    /// <summary>
    /// Elimina \r y caracteres de control ASCII (< 0x20 salvo \t y \n).
    /// Evita que la salida de procesos externos llene el historial de basura.
    /// </summary>
    private static string Sanitize(string s)
    {
        // Ruta rápida: si no hay nada que limpiar, evitar la asignación del StringBuilder.
        var needsClean = false;
        foreach (var c in s)
        {
            if (c == '\r' || (c < 0x20 && c != '\t' && c != '\n'))
            {
                needsClean = true;
                break;
            }
        }
        if (!needsClean) return s;

        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            if (c == '\r') continue;
            if (c < 0x20 && c != '\t' && c != '\n') continue;
            sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>Si el path ya existe, añade -1, -2, ... antes de la extensión.</summary>
    private static string ResolveNonClashingPath(string path)
    {
        if (!File.Exists(path)) return path;

        var dir = Path.GetDirectoryName(path) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(path);
        var ext  = Path.GetExtension(path);

        for (int i = 1; i < 1000; i++)
        {
            var candidate = Path.Combine(dir, $"{name}-{i}{ext}");
            if (!File.Exists(candidate)) return candidate;
        }
        // Último recurso: timestamp.
        return Path.Combine(dir, $"{name}-{DateTime.Now:yyyyMMddHHmmss}{ext}");
    }
}
