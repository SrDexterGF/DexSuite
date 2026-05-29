using System.IO;
using System.Text.Json;
using DexSuite.App.Models;
using Microsoft.Extensions.Logging;

namespace DexSuite.App.Services;

/// <summary>
/// Implementación de <see cref="IPerformanceBaselineService"/> sobre JSON.
/// Guarda un único archivo en <c>%LocalAppData%/DexSuite/baseline.json</c>.
///
/// Criterio de diseño: sencillez por encima de todo. Si el archivo no existe
/// o está corrupto, simplemente se devuelve null y la app funciona sin baseline.
/// </summary>
public sealed class PerformanceBaselineService : IPerformanceBaselineService
{
    private readonly string _path;
    private readonly ILogger<PerformanceBaselineService> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    // ── DTO interno para serialización ────────────────────────────────────────

    private sealed class BaselineDto
    {
        public int Total { get; set; }
        public DateTime Timestamp { get; set; }
        public List<CategoryDto> Categories { get; set; } = new();
    }

    private sealed class CategoryDto
    {
        public string Name   { get; set; } = string.Empty;
        public int    Score  { get; set; }
        public string Detail { get; set; } = string.Empty;
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
    };

    // ── Constructor ───────────────────────────────────────────────────────────

    public PerformanceBaselineService(ILogger<PerformanceBaselineService> logger)
    {
        _logger = logger;
        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DexSuite");
        Directory.CreateDirectory(dataDir);
        _path = Path.Combine(dataDir, "baseline.json");
    }

    // ── Interfaz ──────────────────────────────────────────────────────────────

    public async Task SaveAsync(PerformanceScore score)
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            var dto = new BaselineDto
            {
                Total     = score.Total,
                Timestamp = score.Timestamp,
                Categories = score.Categories
                    .Select(c => new CategoryDto
                    {
                        Name   = c.Name,
                        Score  = c.Score,
                        Detail = c.Detail,
                    }).ToList(),
            };

            var json = JsonSerializer.Serialize(dto, JsonOpts);
            await File.WriteAllTextAsync(_path, json).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo guardar el baseline de rendimiento");
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<PerformanceScore?> LoadAsync()
    {
        if (!File.Exists(_path)) return null;

        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            var json = await File.ReadAllTextAsync(_path).ConfigureAwait(false);
            var dto  = JsonSerializer.Deserialize<BaselineDto>(json, JsonOpts);
            if (dto is null) return null;

            var categories = dto.Categories
                .Select(c => new PerformanceCategoryScore(c.Name, c.Score, c.Detail))
                .ToList();

            return new PerformanceScore(dto.Total, categories, dto.Timestamp);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Baseline corrupto o incompatible; se descarta");
            return null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ClearAsync()
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (File.Exists(_path))
                File.Delete(_path);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo eliminar el archivo de baseline");
        }
        finally
        {
            _lock.Release();
        }
    }
}
