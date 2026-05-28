using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using DexSuite.App.Models;
using Microsoft.Extensions.Logging;

namespace DexSuite.App.Services;

/// <summary>Gestiona el tema visual de la aplicación.</summary>
public sealed class ThemeService : IThemeService
{
    private readonly ILogger<ThemeService> _logger;
    private readonly string _persistPath;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public ThemeService(ILogger<ThemeService> logger)
    {
        _logger = logger;

        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DexSuite");
        Directory.CreateDirectory(dir);
        _persistPath = Path.Combine(dir, "theme.json");
    }

    public AppTheme CurrentTheme { get; private set; } = AppTheme.Default;

    public event EventHandler<AppTheme>? ThemeChanged;

    /// <summary>
    /// Catálogo de temas. El orden define cómo aparecen en el selector.
    /// Los colores de preview se eligen para que el usuario reconozca la paleta
    /// sin tener que aplicarla.
    /// </summary>
    public IReadOnlyList<ThemeDescriptor> AvailableThemes { get; } = new[]
    {
        new ThemeDescriptor(
            AppTheme.Default,
            "Theme.Name.Default", "Theme.Desc.Default",
            Color.FromRgb(0x1B, 0x1B, 0x1B), Color.FromRgb(0x60, 0xCD, 0xFF), Color.FromRgb(0xFF, 0xFF, 0xFF),
            "Free"),

        new ThemeDescriptor(
            AppTheme.Cybernetic,
            "Theme.Name.Cybernetic", "Theme.Desc.Cybernetic",
            Color.FromRgb(0x0F, 0x0F, 0x1F), Color.FromRgb(0x00, 0xB8, 0xD9), Color.FromRgb(0x9D, 0x4E, 0xDD),
            "Pro"),

        new ThemeDescriptor(
            AppTheme.Redline,
            "Theme.Name.Redline", "Theme.Desc.Redline",
            Color.FromRgb(0x13, 0x08, 0x08), Color.FromRgb(0xE6, 0x39, 0x46), Color.FromRgb(0xFF, 0x17, 0x44),
            "Pro"),

        new ThemeDescriptor(
            AppTheme.ZeroLag,
            "Theme.Name.ZeroLag", "Theme.Desc.ZeroLag",
            Color.FromRgb(0x0C, 0x0C, 0x0C), Color.FromRgb(0x60, 0x60, 0x60), Color.FromRgb(0xFF, 0xFF, 0xFF),
            "Pro"),

        new ThemeDescriptor(
            AppTheme.Apex,
            "Theme.Name.Apex", "Theme.Desc.Apex",
            Color.FromRgb(0x13, 0x11, 0x0D), Color.FromRgb(0xD4, 0xAF, 0x37), Color.FromRgb(0xFF, 0xD7, 0x00),
            "Pro"),
    };

    public void ApplyTheme(AppTheme theme)
    {
        var app = Application.Current;
        if (app is null)
        {
            _logger.LogWarning("ApplyTheme llamado sin Application.Current; se ignora.");
            return;
        }

        try
        {
            var newDict = new ResourceDictionary
            {
                Source = new Uri(ThemeResourceUri(theme), UriKind.Absolute),
            };

            // Buscamos el ResourceDictionary que está en el "slot" del tema.
            // Lo marcamos con la convención: su Source contiene "/Themes/".
            var dicts = app.Resources.MergedDictionaries;
            var oldIndex = -1;
            for (int i = 0; i < dicts.Count; i++)
            {
                var src = dicts[i].Source?.OriginalString;
                if (src != null && src.Contains("/Themes/", StringComparison.OrdinalIgnoreCase))
                {
                    oldIndex = i;
                    break;
                }
            }

            if (oldIndex >= 0)
            {
                dicts[oldIndex] = newDict;
            }
            else
            {
                // Si no encontramos slot, lo añadimos al final (mantiene prioridad).
                dicts.Add(newDict);
            }

            CurrentTheme = theme;
            _logger.LogInformation("Tema aplicado: {Theme}", theme);
            ThemeChanged?.Invoke(this, theme);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo aplicar el tema {Theme}", theme);
        }
    }

    /// <summary>
    /// Tema por defecto si no hay archivo de persistencia.
    /// CYBERNETIC es la paleta de marca de DexSuite, así que la mantenemos
    /// en primera ejecución (no rompe la experiencia de usuarios existentes).
    /// </summary>
    private const AppTheme DefaultStartupTheme = AppTheme.Default;

    public AppTheme LoadPersistedTheme()
    {
        try
        {
            if (!File.Exists(_persistPath)) return DefaultStartupTheme;
            var json = File.ReadAllText(_persistPath);
            var dto = JsonSerializer.Deserialize<ThemeDto>(json);
            if (dto is null) return DefaultStartupTheme;
            return Enum.TryParse<AppTheme>(dto.Theme, ignoreCase: true, out var parsed)
                ? parsed
                : DefaultStartupTheme;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo cargar el tema persistido; se usa Cybernetic.");
            return DefaultStartupTheme;
        }
    }

    public async Task PersistAsync()
    {
        await _writeLock.WaitAsync();
        try
        {
            var dto = new ThemeDto { Theme = CurrentTheme.ToString() };
            var json = JsonSerializer.Serialize(dto, JsonOpts);
            await File.WriteAllTextAsync(_persistPath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo persistir el tema");
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private static string ThemeResourceUri(AppTheme theme) => theme switch
    {
        AppTheme.Cybernetic => "pack://application:,,,/Themes/Cybernetic.xaml",
        AppTheme.Redline    => "pack://application:,,,/Themes/Redline.xaml",
        AppTheme.ZeroLag    => "pack://application:,,,/Themes/ZeroLag.xaml",
        AppTheme.Apex       => "pack://application:,,,/Themes/Apex.xaml",
        _                   => "pack://application:,,,/Themes/Default.xaml",
    };

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private sealed class ThemeDto
    {
        public string Theme { get; set; } = nameof(AppTheme.Default);
    }
}
