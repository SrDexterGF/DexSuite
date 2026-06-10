using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DexSuite.App.Models;
using DexSuite.App.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DexSuite.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly INativeModuleRunner _runner;
    private readonly IPerformanceAnalyzer _analyzer;
    private readonly IUpdateService _updateService;
    private readonly ILocalizationService _loc;
    private readonly ILogger<MainViewModel> _logger;
    private readonly IQuickCleanService _quickClean;
    private readonly ISystemInfoService _systemInfo;
    private readonly IAppLogService _appLog;
    private readonly IPerformanceBaselineService _baseline;
    private readonly IRestorePointService _restorePoint;
    private readonly IThemeService _themeService;
    private readonly ISettingsService _settingsService;
    private readonly IWingetService _winget;
    private readonly ISecurityCheckService _security;
    private readonly IChangeTrackingService _changes;
    private readonly IModuleStateService _moduleState;
    private readonly IBugReportService _bugReport;
    private readonly IAppSelfCleanupService _selfCleanup;
    private readonly ILicenseService _license;
    // Resolver de ventanas transient (GameSelectorWindow). El alternativo
    // era inyectar Func<GameSelectorWindow>; usar IServiceProvider mantiene
    // la firma simple si añadimos más diálogos en el futuro.
    private readonly IServiceProvider _services;

    // Mientras es false, los OnXxxChanged no llaman a PersistSettings (evita
    // escrituras espurias al hidratar valores desde settings.json al arrancar).
    private bool _settingsHydrated;

    // CTS del run en curso, para que el botón Cancelar pueda matarlo.
    private CancellationTokenSource? _runCts;

    // Acumulador de líneas pendientes que aún no se han volcado a OutputLog.
    private readonly StringBuilder _pendingBuffer = new();
    private readonly object _bufferLock = new();
    private const int MaxLogChars = 80_000;

    // Step progress counters (written on background thread inside await foreach, read on UI thread via Dispatcher).
    private int _runModuleTotal;
    private int _runModuleIndex;

    public ObservableCollection<ModuleItemViewModel> Modules { get; } = new();
    public ObservableCollection<ModuleItemViewModel> FreeModules { get; } = new();
    public ObservableCollection<ModuleItemViewModel> AdvancedModules { get; } = new();
    /// <summary>Módulos Pro estándar (excluye Extras/Gaming).</summary>
    public ObservableCollection<ModuleItemViewModel> ProModules { get; } = new();
    /// <summary>Módulos Pro de categoría Extras (videojuegos), con sección visual propia.</summary>
    public ObservableCollection<ModuleItemViewModel> ProExtraModules { get; } = new();

    [ObservableProperty]
    private string outputLog = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    private bool isRunning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    private bool isQuickCleaning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    private bool isUpdatingApps;

    /// <summary>True cuando la app no está ejecutando módulos, analizando, limpiando ni creando restore point.</summary>
    public bool IsIdle => !IsRunning && !IsAnalyzing && !IsQuickCleaning && !IsCreatingRestorePoint && !IsUpdatingApps;

    /// <summary>True si winget está disponible en el sistema (habilita el botón de actualizar apps).</summary>
    public bool IsWingetAvailable => _winget.IsAvailable;

    /// <summary>True cuando hay una actualización de DexSuite disponible (tiñe la flecha del sidebar).</summary>
    public bool IsUpdateAvailable => HasAvailableUpdate;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    /// <summary>Texto "Módulo X de Y" visible en el pie mientras hay una ejecución en curso.</summary>
    [ObservableProperty]
    private string runProgressText = string.Empty;

    /// <summary>Porcentaje completado (0–100) para la barra de progreso del pie.</summary>
    [ObservableProperty]
    private double runProgressPercent = 0.0;

    // Helper i18n
    // T() = "translate", abreviado para no inflar las asignaciones de StatusMessage.

    /// <summary>Traduce una clave i18n al idioma activo.</summary>
    private string T(string key) => _loc.Get(key);

    /// <summary>Traduce una clave i18n y aplica string.Format con los argumentos.</summary>
    private string T(string key, params object?[] args)
        => string.Format(_loc.Get(key), args);

    // Navegación entre secciones (sidebar)

    /// <summary>
    /// Bloqueo global de la sección "Puesta a Punto". Mientras sea true, la
    /// sección se muestra con un overlay "Coming Soon" y su contenido no es
    /// interactivo, independientemente de la versión o el plan del usuario.
    /// </summary>
    public static bool TuningComingSoon { get; } = true;

    /// <summary>Espejo de instancia de <see cref="TuningComingSoon"/> para binding XAML directo.</summary>
    public bool IsTuningComingSoon => TuningComingSoon;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHomeView))]
    [NotifyPropertyChangedFor(nameof(IsModulesView))]
    [NotifyPropertyChangedFor(nameof(IsLogView))]
    [NotifyPropertyChangedFor(nameof(IsSpecsView))]
    [NotifyPropertyChangedFor(nameof(IsRestoreView))]
    [NotifyPropertyChangedFor(nameof(IsTuningView))]
    [NotifyPropertyChangedFor(nameof(IsSettingsView))]
    [NotifyPropertyChangedFor(nameof(IsUpdatesView))]
    [NotifyPropertyChangedFor(nameof(IsAboutView))]
    private AppSection currentSection = AppSection.Home;

    public bool IsHomeView     => CurrentSection == AppSection.Home;
    public bool IsModulesView  => CurrentSection == AppSection.Modules;
    public bool IsLogView      => CurrentSection == AppSection.Log;
    public bool IsSpecsView    => CurrentSection == AppSection.Specs;
    public bool IsRestoreView  => CurrentSection == AppSection.Restore;
    public bool IsTuningView   => CurrentSection == AppSection.Tuning;
    public bool IsSettingsView => CurrentSection == AppSection.Settings;
    public bool IsUpdatesView  => CurrentSection == AppSection.Updates;
    public bool IsAboutView    => CurrentSection == AppSection.About;

    [RelayCommand]
    private void Navigate(string sectionName)
    {
        if (Enum.TryParse<AppSection>(sectionName, ignoreCase: true, out var section))
        {
            CurrentSection = section;
            if (section == AppSection.Specs && SystemSpecs is null)
                _ = LoadSystemInfoAsync();
            if (section == AppSection.Log)
                _ = RefreshAppLogAsync();
            if (section == AppSection.Restore)
                _ = RefreshChangesAsync();
        }
    }

    /// <summary>
    /// Abre una URL en el navegador por defecto. Lo usan los botones del
    /// instalador de apps de la sección Tuning (operativos cuando la sección
    /// se desbloquee; mientras está en Coming Soon el overlay impide pulsarlos).
    /// Registra cada apertura en el historial interno (AppLog).
    ///
    /// Cuando la sección Tuning se desbloquee y se implementen handlers reales
    /// (cambio de resolución, frecuencia, aceleración de ratón…), cada handler
    /// debe usar IChangeTrackingService para que el ajuste aparezca en
    /// "Revertir cambios" y emitir un evento via _appLog igual que hacen los
    /// módulos en Services/CleanupModules/.
    /// </summary>
    [RelayCommand]
    private async Task OpenDownloadUrlAsync(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;

        // Validar que el esquema sea seguro antes de lanzar como admin.
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps &&
             uri.Scheme != Uri.UriSchemeHttp  &&
             uri.Scheme != "mailto"))
        {
            _logger.LogWarning("URL rechazada por esquema no permitido: {Url}", url);
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            });
            await _appLog.InfoAsync(AppLogCategory.App,
                T("Tuning.Log.UrlOpened", url));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo abrir la URL de descarga {Url}", url);
            await _appLog.ErrorAsync(AppLogCategory.App,
                T("Tuning.Log.UrlFailed", url, ex.Message));
        }
    }

    // Especificaciones del sistema

    [ObservableProperty]
    private DexSuite.App.Models.SystemInfo? systemSpecs;

    [ObservableProperty]
    private bool isLoadingSpecs;

    [RelayCommand]
    private async Task LoadSystemInfoAsync()
    {
        IsLoadingSpecs = true;
        try
        {
            SystemSpecs = await _systemInfo.GetSystemInfoAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudieron obtener las especificaciones del sistema");
        }
        finally
        {
            IsLoadingSpecs = false;
        }
    }

    // Historial interno (SQLite)

    /// <summary>Entradas del historial cargadas, más recientes primero.</summary>
    public ObservableCollection<LogEntry> AppLogEntries { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAppLogEntries))]
    [NotifyPropertyChangedFor(nameof(IsAppLogEmpty))]
    private bool isLoadingAppLog;

    public bool HasAppLogEntries => AppLogEntries.Count > 0;
    public bool IsAppLogEmpty    => !IsLoadingAppLog && AppLogEntries.Count == 0;

    [RelayCommand]
    private async Task RefreshAppLogAsync()
    {
        IsLoadingAppLog = true;
        try
        {
            var items = await _appLog.GetRecentAsync(500);
            AppLogEntries.Clear();
            foreach (var e in items) AppLogEntries.Add(e);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo cargar el historial interno");
        }
        finally
        {
            IsLoadingAppLog = false;
            // CollectionChanged ya notificó HasAppLogEntries / IsAppLogEmpty.
            // IsLoadingAppLog tiene [NotifyPropertyChangedFor] para ambas propiedades.
        }
    }

    [RelayCommand]
    private async Task ClearAppLogAsync()
    {
        try
        {
            var n = await _appLog.ClearAllAsync();
            AppLogEntries.Clear();
            StatusMessage = T("Log.Cleared", n);
            await _appLog.InfoAsync(AppLogCategory.App, T("Log.Event.HistoryCleared", n));
        }
        catch (Exception ex)
        {
            StatusMessage = T("Log.ClearError", ex.Message);
            _logger.LogError(ex, "No se pudo vaciar el historial interno");
        }
        // CollectionChanged notifica HasAppLogEntries / IsAppLogEmpty al hacer Clear().
    }

    [RelayCommand]
    private async Task ExportAppLogAsync()
    {
        try
        {
            var dir = Path.Combine(LogsFolder, "history-exports");
            var name = $"dexsuite-history-{DateTime.Now:yyyyMMdd-HHmmss}.txt";
            var target = Path.Combine(dir, name);
            var finalPath = await _appLog.ExportToTextAsync(target);
            StatusMessage = T("Log.ExportSuccess", finalPath);

            // Abrir la carpeta destino para que el usuario localice el archivo.
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{finalPath}\"",
                    UseShellExecute = true,
                });
            }
            catch { /* no crítico */ }
        }
        catch (Exception ex)
        {
            StatusMessage = T("Log.ExportError", ex.Message);
            _logger.LogError(ex, "No se pudo exportar el historial interno");
        }
    }

    private void OnAppLogEntryAdded(object? sender, LogEntry entry)
    {
        // El evento se dispara desde un hilo de pool; marshalling al UI thread.
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            AppLogEntries.Insert(0, entry);
            // Tope visual para no inflar la UI sin límite. Persistido sigue intacto.
            const int MaxInMemory = 500;
            while (AppLogEntries.Count > MaxInMemory)
                AppLogEntries.RemoveAt(AppLogEntries.Count - 1);
            // CollectionChanged dispara HasAppLogEntries / IsAppLogEmpty automáticamente.
        });
    }

    // Búsqueda de módulos

    public ObservableCollection<ModuleItemViewModel> SearchResults { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSearchActive))]
    [NotifyPropertyChangedFor(nameof(IsNormalView))]
    [NotifyPropertyChangedFor(nameof(SearchResultsLabel))]
    [NotifyPropertyChangedFor(nameof(HasSearchResults))]
    private string searchText = string.Empty;

    public bool IsSearchActive   => !string.IsNullOrWhiteSpace(SearchText);
    public bool IsNormalView     => string.IsNullOrWhiteSpace(SearchText);
    public bool HasSearchResults => SearchResults.Count > 0;

    /// <summary>Etiqueta de resultados: cuenta o "sin resultados", localizada.</summary>
    public string SearchResultsLabel => IsSearchActive
        ? (HasSearchResults
            ? T("Search.ResultsCount", SearchResults.Count, SearchText.Trim())
            : T("Search.NoResults", SearchText.Trim()))
        : string.Empty;

    partial void OnSearchTextChanged(string value)
    {
        SearchResults.Clear();
        var q = value?.Trim() ?? string.Empty;
        if (!string.IsNullOrEmpty(q))
        {
            foreach (var m in Modules)
            {
                if (m.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    m.Description.Contains(q, StringComparison.OrdinalIgnoreCase))
                    SearchResults.Add(m);
            }
        }
        // Notify computed after collection rebuild
        OnPropertyChanged(nameof(HasSearchResults));
        OnPropertyChanged(nameof(SearchResultsLabel));
    }

    [RelayCommand]
    private void ClearSearch() => SearchText = string.Empty;

    // Acciones rápidas sobre la lista de módulos

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var m in Modules)
            if (!m.IsLocked) m.IsEnabled = true;
    }

    [RelayCommand]
    private void DeselectAll()
    {
        foreach (var m in Modules) m.IsEnabled = false;
    }

    [RelayCommand]
    private void SelectRecommended()
    {
        var tier = UserTierEnum;
        foreach (var m in Modules)
        {
            if (m.IsLocked) { m.IsEnabled = false; continue; }
            m.IsEnabled = IsRecommendedForTier(m.Module.Id, tier);
        }
    }

    // Conjuntos de módulos recomendados por tier.
    // Free: limpieza segura y reversible de bajo impacto.
    // Avanzado: añade red, caché de apps y reparación del sistema.
    // Pro: añade privacidad, red Ethernet y TRIM de SSD.
    // Nunca se auto-seleccionan módulos de impacto Extremo (M15) ni los
    // que modifican drivers/seguridad de forma agresiva (M11, M13, M17, M19).
    private static bool IsRecommendedForTier(int id, ModuleTier tier) => tier switch
    {
        ModuleTier.Free     => id is 1 or 2 or 5 or 7,
        ModuleTier.Advanced => id is 1 or 2 or 5 or 7 or 8 or 9 or 10,
        ModuleTier.Pro      => id is 1 or 2 or 5 or 7 or 8 or 9 or 10 or 14 or 16 or 18,
        _                   => false,
    };

    /// <summary>
    /// Construye el mapa moduleId → sub-opciones marcadas para pasarlo al runner.
    /// En vista simple devuelve null (cada módulo ejecuta todas sus operaciones).
    /// En vista avanzada, por cada módulo seleccionado con sub-opciones, recoge
    /// solo las que el usuario dejó marcadas.
    /// </summary>
    private IReadOnlyDictionary<int, IReadOnlySet<string>>? BuildSubOptionsMap(IReadOnlyList<int> selected)
    {
        if (!IsAdvancedModuleView) return null;

        var map = new Dictionary<int, IReadOnlySet<string>>();
        foreach (var m in Modules.Where(m => m.IsEnabled && m.HasSubOptions && selected.Contains(m.Id)))
        {
            var enabled = m.SubOptions.Where(s => s.IsEnabled).Select(s => s.Id)
                .ToHashSet(StringComparer.Ordinal);
            map[m.Id] = enabled;
        }
        return map;
    }

    // Idioma

    /// <summary>Idiomas disponibles en la app (10).</summary>
    public IReadOnlyList<LanguageOption> AvailableLanguages => _loc.AvailableLanguages;

    /// <summary>Idioma activo, two-way con el servicio de localización.</summary>
    public string CurrentLanguage
    {
        get => _loc.CurrentLanguage;
        set
        {
            if (_loc.CurrentLanguage == value) return;
            _loc.CurrentLanguage = value;
            OnPropertyChanged();
        }
    }

    // Ajustes

    /// <summary>Si la lista de módulos arranca con los recomendados ya marcados.</summary>
    [ObservableProperty]
    private bool autoSelectRecommended = false;

    /// <summary>Si al pulsar Ejecutar saltamos automáticamente a la vista de Registro.</summary>
    [ObservableProperty]
    private bool jumpToLogOnRun = false;

    /// <summary>
    /// Vista de módulos por defecto: false = simple (opciones agrupadas),
    /// true = avanzada (cada ajuste individual seleccionable por separado).
    /// </summary>
    [ObservableProperty]
    private bool isAdvancedModuleView = false;

    /// <summary>Avisar antes de ejecutar módulos no reversibles (futuro).</summary>
    [ObservableProperty]
    private bool warnBeforeNonReversible = true;

    /// <summary>Crear punto de restauración automáticamente antes de ejecutar módulos.</summary>
    [ObservableProperty]
    private bool createRestorePointBeforeRun = false;

    /// <summary>True mientras se está creando el punto de restauración.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    private bool isCreatingRestorePoint;

    /// <summary>Mensaje del último intento de restauración (ok o error).</summary>
    [ObservableProperty]
    private string restorePointStatusMessage = string.Empty;

    /// <summary>Mostrar notificación de Windows al terminar (futuro).</summary>
    [ObservableProperty]
    private bool notifyOnFinish = true;

    /// <summary>
    /// Tier activo del usuario. Solo lo escribe el ViewModel desde el evento
    /// <c>ILicenseService.TierChanged</c> — la UI lo muestra pero no permite
    /// cambiarlo libremente: para subir de tier hay que activar una clave válida.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UserTierEnum))]
    private string userTier = "Free";

    /// <summary>HWID del equipo, formato visual XXXX-XXXX-XXXX-XXXX-XXXX.</summary>
    [ObservableProperty]
    private string hardwareId = string.Empty;

    /// <summary>Clave de activación que el usuario está pegando en la UI.</summary>
    [ObservableProperty]
    private string activationKeyInput = string.Empty;

    /// <summary>Mensaje de estado bajo el botón Activar (éxito / error / tier actual).</summary>
    [ObservableProperty]
    private string licenseStatusMessage = string.Empty;

    /// <summary>True mientras la activación está en curso (deshabilita el botón).</summary>
    [ObservableProperty]
    private bool isActivatingLicense;

    /// <summary>Convierte el enum <see cref="ModuleTier"/> al string que usa la UI.</summary>
    private static string TierToString(ModuleTier tier) => tier switch
    {
        ModuleTier.Pro      => "Pro",
        ModuleTier.Advanced => "Avanzado",
        _                   => "Free",
    };

    [RelayCommand]
    private void CopyHardwareId()
    {
        try
        {
            System.Windows.Clipboard.SetText(HardwareId);
            LicenseStatusMessage = T("License.HwidCopied");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo copiar el HWID al portapapeles");
        }
    }

    private const int LicenseMaxAttempts  = 5;
    private const int LicenseLockMinutes  = 15;

    private bool CanActivateLicense() => !IsActivatingLicense;

    [RelayCommand(CanExecute = nameof(CanActivateLicense))]
    private async Task ActivateLicenseAsync()
    {
        if (string.IsNullOrWhiteSpace(ActivationKeyInput))
        {
            LicenseStatusMessage = T("License.Activation.Empty");
            return;
        }

        // Rate limiting: comprobar si hay bloqueo activo
        var settings = _settingsService.Load();
        if (settings.LicenseLockedUntil is string lockedStr &&
            DateTime.TryParse(lockedStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var lockedUntil) &&
            DateTime.UtcNow < lockedUntil)
        {
            var mins = (int)Math.Ceiling((lockedUntil - DateTime.UtcNow).TotalMinutes);
            LicenseStatusMessage = T("License.Activation.RateLimited", mins);
            return;
        }

        IsActivatingLicense = true;
        ActivateLicenseCommand.NotifyCanExecuteChanged();
        try
        {
            var result = await _license.ActivateAsync(ActivationKeyInput);
            if (result.Success)
            {
                settings.LicenseFailedAttempts = 0;
                settings.LicenseLockedUntil = null;
                _settingsService.ScheduleSave(settings);

                LicenseStatusMessage = T("License.Activation.Success", TierToString(result.Tier));
                ActivationKeyInput = string.Empty;
                await _appLog.SuccessAsync(AppLogCategory.Settings,
                    T("Log.Event.LicenseActivated", TierToString(result.Tier)));
            }
            else
            {
                settings.LicenseFailedAttempts++;
                if (settings.LicenseFailedAttempts >= LicenseMaxAttempts)
                {
                    settings.LicenseLockedUntil = DateTime.UtcNow.AddMinutes(LicenseLockMinutes).ToString("O");
                    settings.LicenseFailedAttempts = 0;
                    LicenseStatusMessage = T("License.Activation.RateLimited", LicenseLockMinutes);
                }
                else
                {
                    var remaining = LicenseMaxAttempts - settings.LicenseFailedAttempts;
                    LicenseStatusMessage = T("License.Activation.FailedWithAttempts",
                        result.Message ?? "?", remaining);
                }
                _settingsService.ScheduleSave(settings);
                await _appLog.WarningAsync(AppLogCategory.Settings,
                    T("Log.Event.LicenseActivationFailed", result.Message ?? "?"));
            }
        }
        finally
        {
            IsActivatingLicense = false;
            ActivateLicenseCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand]
    private async Task DeactivateLicenseAsync()
    {
        await _license.DeactivateAsync();
        LicenseStatusMessage = T("License.Status.Free");
        await _appLog.InfoAsync(AppLogCategory.Settings, T("Log.Event.LicenseDeactivated"));
    }

    /// <summary>Tier activo como enum comparable con <see cref="ModuleTier"/>.</summary>
    public ModuleTier UserTierEnum => UserTier switch
    {
        "Avanzado" => ModuleTier.Advanced,
        "Pro"      => ModuleTier.Pro,
        _          => ModuleTier.Free,
    };

    /// <summary>
    /// Cada vez que cambia el tier (solo lo escribe el VM desde ILicenseService.TierChanged),
    /// recalcula qué módulos quedan bloqueados y refresca el selector de temas.
    /// Ya NO persiste a settings.json: la fuente de verdad es la licencia firmada.
    /// </summary>
    partial void OnUserTierChanged(string value)
    {
        UpdateModuleLockStates();
        RefreshThemeItems(); // El bloqueo del selector de temas también depende del tier.
        OpenGameSelectorCommand.NotifyCanExecuteChanged();
        _ = _appLog.InfoAsync(AppLogCategory.Settings, T("Log.Event.TierChanged", value));
    }

    // Persistencia (todos los toggles de Ajustes llaman aquí)

    partial void OnAutoSelectRecommendedChanged(bool value)       => PersistSettings();
    partial void OnJumpToLogOnRunChanged(bool value)              => PersistSettings();
    partial void OnIsAdvancedModuleViewChanged(bool value)        => PersistSettings();
    partial void OnWarnBeforeNonReversibleChanged(bool value)     => PersistSettings();
    partial void OnCreateRestorePointBeforeRunChanged(bool value) => PersistSettings();
    partial void OnNotifyOnFinishChanged(bool value)              => PersistSettings();
    partial void OnAutoUpdateEnabledChanged(bool value)           => PersistSettings();
    partial void OnMinimizeToTrayChanged(bool value)              => PersistSettings();
    partial void OnUpdateChannelChanged(string value)             => PersistSettings();
    partial void OnShowGamingDisclaimerChanged(bool value)        => PersistSettings();

    /// <summary>
    /// Toma un snapshot del estado actual y lo manda a guardar (con debounce).
    /// No-op durante la hidratación inicial para no escribir N veces seguidas.
    /// </summary>
    private void PersistSettings()
    {
        if (!_settingsHydrated) return;
        _settingsService.ScheduleSave(new AppSettings
        {
            Language                    = CurrentLanguage,
            UpdateChannel               = UpdateChannel,
            AutoSelectRecommended       = AutoSelectRecommended,
            JumpToLogOnRun              = JumpToLogOnRun,
            WarnBeforeNonReversible     = WarnBeforeNonReversible,
            CreateRestorePointBeforeRun = CreateRestorePointBeforeRun,
            NotifyOnFinish              = NotifyOnFinish,
            AutoUpdateEnabled           = AutoUpdateEnabled,
            MinimizeToTray              = MinimizeToTray,
            ShowGamingDisclaimer        = ShowGamingDisclaimer,
            IsAdvancedModuleView        = IsAdvancedModuleView,
        });
    }

    private void UpdateModuleLockStates()
    {
        var currentTier = UserTierEnum;
        foreach (var m in Modules)
            m.IsLocked = m.Module.Tier > currentTier;
    }

    /// <summary>Opciones disponibles para el ComboBox de canal de actualización.</summary>
    public IReadOnlyList<string> AvailableChannels { get; } = new[] { "Stable", "Beta" };

    /// <summary>Carpeta donde Serilog escribe los logs (calculada al construir el VM).</summary>
    public string LogsFolder { get; }

    // Temas

    /// <summary>Items observables para el selector de temas en Ajustes.</summary>
    public ObservableCollection<ThemeItemViewModel> ThemeItems { get; } = new();

    /// <summary>Temas de videojuegos mostrados en el Expander "Temas 😉".</summary>
    public ObservableCollection<ThemeItemViewModel> GameThemeItems { get; } = new();

    [ObservableProperty]
    private AppTheme currentTheme;

    /// <summary>
    /// Refresca el flag IsActive/IsUnlocked de cada item del selector.
    /// Llamar cuando cambia <see cref="CurrentTheme"/> o <see cref="UserTier"/>.
    /// </summary>
    private void RefreshThemeItems()
    {
        foreach (var item in ThemeItems)
        {
            if (item.IsComingSoon) continue;   // placeholder, no cambia de estado
            item.IsActive = item.Theme == CurrentTheme;
            item.IsUnlocked = IsThemeUnlocked(item.MinTier);
        }
        foreach (var item in GameThemeItems)
        {
            item.IsActive = item.Theme == CurrentTheme;
            item.IsUnlocked = IsThemeUnlocked(item.MinTier);
        }
    }

    /// <summary>
    /// Aplica un tema y lo persiste a disco. Si el plan del usuario no lo
    /// desbloquea, no hace nada (la UI ya muestra el candado).
    /// </summary>
    [RelayCommand]
    private async Task SelectThemeAsync(ThemeItemViewModel? item)
    {
        if (item is null) return;
        if (item.IsComingSoon) return;   // tarjeta placeholder, no aplicable
        if (!item.IsUnlocked) return;
        if (_themeService.CurrentTheme == item.Theme) return;

        _themeService.ApplyTheme(item.Theme);
        await _themeService.PersistAsync();

        var localizedName = T(item.NameKey);
        await _appLog.InfoAsync(AppLogCategory.Settings,
            T("Log.Event.ThemeChanged", localizedName));
    }

    /// <summary>
    /// True si el plan actual del usuario desbloquea un tema cuyo requisito
    /// es <paramref name="minTier"/>. Jerarquía: Free &lt; Avanzado &lt; Pro.
    /// </summary>
    public bool IsThemeUnlocked(string minTier)
    {
        var current = TierRank(UserTier);
        var required = TierRank(minTier);
        return current >= required;
    }

    private static int TierRank(string tier) => tier switch
    {
        "Pro" => 3,
        "Avanzado" => 2,
        "Free" => 1,
        _ => 0,
    };

    partial void OnCurrentThemeChanged(AppTheme value) => RefreshThemeItems();

    [RelayCommand]
    private void OpenLogsFolder()
    {
        try
        {
            if (!Directory.Exists(LogsFolder)) Directory.CreateDirectory(LogsFolder);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = LogsFolder,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            StatusMessage = T("Status.FolderOpenError", ex.Message);
        }
    }

    [RelayCommand]
    private void ResetSettings()
    {
        AutoSelectRecommended = false;
        JumpToLogOnRun = false;
        WarnBeforeNonReversible = true;
        CreateRestorePointBeforeRun = false;
        NotifyOnFinish = true;
        AutoUpdateEnabled = false;
        MinimizeToTray = false;
        // UserTier NO se resetea aquí: lo controla únicamente la licencia activa.
        // Resetear configs no debe regalar Pro.
        StatusMessage = T("Status.SettingsReset");
        _ = _appLog.InfoAsync(AppLogCategory.Settings, T("Log.Event.SettingsReset"));
    }

    /// <summary>
    /// Reabre el diálogo de Terms of Use en modo solo lectura (sin aceptar/declinar).
    /// No lee ni modifica el flag de aceptación de settings.json.
    /// </summary>
    [RelayCommand]
    private void ShowTerms()
    {
        var terms = new DexSuite.App.TermsWindow(readOnly: true)
        {
            Owner = Application.Current?.MainWindow,
        };
        terms.ShowDialog();
    }

    // Punto de restauración

    /// <summary>
    /// Crea un punto de restauración de Windows de forma manual (desde Ajustes).
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCreateRestorePoint))]
    private async Task CreateRestorePointAsync()
    {
        IsCreatingRestorePoint = true;
        CreateRestorePointCommand.NotifyCanExecuteChanged();
        RunCommand.NotifyCanExecuteChanged();
        StatusMessage = T("Status.CreatingRestorePoint");
        RestorePointStatusMessage = string.Empty;

        var desc = T("RestorePoint.Description");
        try
        {
            var result = await _restorePoint.CreateAsync(desc);
            if (result.Success)
            {
                StatusMessage = T("Status.RestorePointCreated");
                RestorePointStatusMessage = T("RestorePoint.Success");
                await _appLog.SuccessAsync(AppLogCategory.Settings,
                    T("Log.Event.RestorePointCreated", desc));
            }
            else
            {
                StatusMessage = T("Status.RestorePointFailed", result.Message);
                RestorePointStatusMessage = T("RestorePoint.Failed", result.Message);
                await _appLog.WarningAsync(AppLogCategory.Settings,
                    T("Log.Event.RestorePointFailed", result.Message));
            }
        }
        catch (Exception ex)
        {
            StatusMessage = T("Status.RestorePointFailed", ex.Message);
            RestorePointStatusMessage = T("RestorePoint.Failed", ex.Message);
            _logger.LogError(ex, "No se pudo crear el punto de restauración");
        }
        finally
        {
            IsCreatingRestorePoint = false;
            CreateRestorePointCommand.NotifyCanExecuteChanged();
            RunCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanCreateRestorePoint() => !IsRunning && !IsAnalyzing && !IsCreatingRestorePoint;

    /// <summary>
    /// Intenta crear un punto de restauración antes de ejecutar, si la opción está activa.
    /// Devuelve true si se debe continuar con la ejecución (éxito o usuario acepta el riesgo).
    /// </summary>
    private async Task<bool> TryAutoRestorePointAsync(int moduleCount)
    {
        if (!CreateRestorePointBeforeRun) return true;

        IsCreatingRestorePoint = true;
        RunCommand.NotifyCanExecuteChanged();
        StatusMessage = T("Status.CreatingRestorePoint");

        var desc = T("RestorePoint.AutoDescription", moduleCount);
        try
        {
            var result = await _restorePoint.CreateAsync(desc);
            if (result.Success)
            {
                await _appLog.SuccessAsync(AppLogCategory.Settings,
                    T("Log.Event.RestorePointCreated", desc));
                return true;
            }

            // Si falla, loggeamos como warning pero NO abortamos la ejecución.
            // El usuario quería optimizar; no lo bloqueamos por un restore fallido.
            await _appLog.WarningAsync(AppLogCategory.Settings,
                T("Log.Event.RestorePointFailed", result.Message));
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo crear restore point automático");
            await _appLog.WarningAsync(AppLogCategory.Settings,
                T("Log.Event.RestorePointFailed", ex.Message));
            return true; // continuamos igualmente
        }
        finally
        {
            IsCreatingRestorePoint = false;
            RunCommand.NotifyCanExecuteChanged();
        }
    }

    // Actualizaciones

    /// <summary>Versión instalada actual, reportada por Velopack (o "0.1.0" en dev).</summary>
    public string CurrentVersion => _updateService.CurrentVersion;

    /// <summary>Título localizado del changelog en Acerca de, p.ej. "Novedades en v0.1.0".</summary>
    public string ChangelogTitle => T("About.Changelog.Title", CurrentVersion);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LastUpdateCheckLabel))]
    private string? lastUpdateCheck = null;

    /// <summary>Etiqueta localizada "Última comprobación: ...".</summary>
    public string LastUpdateCheckLabel =>
        T("Updates.LastCheck", LastUpdateCheck ?? T("Common.Never"));

    [ObservableProperty]
    private bool autoUpdateEnabled = false;

    /// <summary>Si al minimizar la ventana se oculta a la bandeja del sistema.</summary>
    [ObservableProperty]
    private bool minimizeToTray = false;

    /// <summary>Si se muestra el aviso de gaming antes de abrir el selector de juegos.
    /// Default true; el usuario puede desactivarlo desde el propio diálogo.</summary>
    [ObservableProperty]
    private bool showGamingDisclaimer = true;

    [ObservableProperty]
    private string updateChannel = "Stable";

    /// <summary>Versión disponible para descargar, null si no hay actualización.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAvailableUpdate))]
    [NotifyPropertyChangedFor(nameof(IsUpdateAvailable))]
    [NotifyPropertyChangedFor(nameof(AvailableUpdateVersionLabel))]
    private string? availableUpdateVersion;

    /// <summary>True cuando hay una actualización descargable lista.</summary>
    public bool HasAvailableUpdate => AvailableUpdateVersion is not null;

    /// <summary>Etiqueta localizada "Versión v{x} lista para descargar..."</summary>
    public string AvailableUpdateVersionLabel
        => AvailableUpdateVersion is null
            ? string.Empty
            : T("Updates.NewVersionReady", AvailableUpdateVersion);

    [ObservableProperty]
    private int updateDownloadProgress;

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        StatusMessage = T("Status.Searching");
        try
        {
            var newVersion = await _updateService.CheckForUpdatesAsync();
            LastUpdateCheck = DateTime.Now.ToString("dd/MM/yyyy HH:mm");

            if (newVersion is not null)
            {
                AvailableUpdateVersion = newVersion;
                StatusMessage = T("Status.NewVersion", newVersion);
                await _appLog.SuccessAsync(AppLogCategory.Update,
                    T("Log.Event.UpdateFound", newVersion));
            }
            else if (!_updateService.IsInstalledBuild)
            {
                StatusMessage = T("Status.DevMode");
                LastUpdateCheck = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
            }
            else
            {
                AvailableUpdateVersion = null;
                StatusMessage = T("Status.UpToDate", CurrentVersion);
                await _appLog.InfoAsync(AppLogCategory.Update,
                    T("Log.Event.UpdateNone", CurrentVersion));
            }
        }
        catch (Exception ex)
        {
            StatusMessage = T("Status.UpdateError", ex.Message);
            _logger.LogError(ex, "Fallo al buscar actualizaciones");
            await _appLog.ErrorAsync(AppLogCategory.Update,
                T("Log.Event.UpdateCheckFailed", ex.Message), ex.ToString());
        }
        ApplyUpdateCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(HasAvailableUpdate))]
    private async Task ApplyUpdateAsync()
    {
        StatusMessage = T("Status.Downloading", 0);
        UpdateDownloadProgress = 0;
        try
        {
            var progress = new Progress<int>(p =>
            {
                UpdateDownloadProgress = p;
                StatusMessage = T("Status.Downloading", p);
            });
            await _updateService.DownloadAndApplyAsync(progress);
            // Si llegamos aquí es que no se reinició (entorno no instalado).
            StatusMessage = T("Status.UpdateDownloaded");
            await _appLog.SuccessAsync(AppLogCategory.Update,
                T("Log.Event.UpdateApplied", AvailableUpdateVersion ?? "?"));
        }
        catch (Exception ex)
        {
            StatusMessage = T("Status.UpdateApplyError", ex.Message);
            _logger.LogError(ex, "Fallo al aplicar actualización");
            await _appLog.ErrorAsync(AppLogCategory.Update,
                T("Log.Event.UpdateApplyFailed", ex.Message), ex.ToString());
        }
    }

    // Test de rendimiento

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasScoreBefore))]
    [NotifyPropertyChangedFor(nameof(HasScoreAfter))]
    [NotifyPropertyChangedFor(nameof(HasComparison))]
    [NotifyPropertyChangedFor(nameof(ScoreDelta))]
    [NotifyPropertyChangedFor(nameof(ScoreDeltaLabel))]
    [NotifyPropertyChangedFor(nameof(BaselineTimestampLabel))]
    private PerformanceScore? scoreBefore;

    /// <summary>True cuando el baseline fue cargado de disco (no medido en esta sesión).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BaselineTimestampLabel))]
    private bool isBaselineFromFile;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasScoreAfter))]
    [NotifyPropertyChangedFor(nameof(HasComparison))]
    [NotifyPropertyChangedFor(nameof(ScoreDelta))]
    [NotifyPropertyChangedFor(nameof(ScoreDeltaLabel))]
    private PerformanceScore? scoreAfter;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    private bool isAnalyzing;

    public bool HasScoreBefore => ScoreBefore is not null;
    public bool HasScoreAfter => ScoreAfter is not null;
    public bool HasComparison => ScoreBefore is not null && ScoreAfter is not null;
    public int ScoreDelta => HasComparison ? ScoreAfter!.Total - ScoreBefore!.Total : 0;
    public string ScoreDeltaLabel => HasComparison
        ? (ScoreDelta >= 0 ? $"+{ScoreDelta}" : ScoreDelta.ToString())
        : string.Empty;

    /// <summary>
    /// Etiqueta que indica cuándo se obtuvo el baseline y si procede del disco.
    /// Ejemplo: "Guardado el 12/05 14:30" o "Medido el 12/05 14:30".
    /// </summary>
    public string BaselineTimestampLabel
    {
        get
        {
            if (ScoreBefore is null) return string.Empty;
            var ts = ScoreBefore.Timestamp.ToString("dd/MM/yyyy HH:mm");
            return IsBaselineFromFile
                ? T("Score.LoadedAt", ts)
                : T("Score.MeasuredAt", ts);
        }
    }

    [RelayCommand(CanExecute = nameof(CanAnalyze))]
    private async Task AnalyzeAsync()
    {
        IsAnalyzing = true;
        AnalyzeCommand.NotifyCanExecuteChanged();
        ResetScoresCommand.NotifyCanExecuteChanged();
        RunCommand.NotifyCanExecuteChanged();

        StatusMessage = T("Status.Analyzing");

        try
        {
            var score = await _analyzer.AnalyzeAsync();
            if (ScoreBefore is null || IsBaselineFromFile)
            {
                // Primera medición de la sesión (o el baseline venía del disco:
                // ahora lo sustituimos por una medición en vivo).
                IsBaselineFromFile = false;
                ScoreAfter = null;
                ScoreBefore = score;
                StatusMessage = T("Status.AnalysisInitial", score.Total, T(score.Verdict));
                await _appLog.InfoAsync(AppLogCategory.Analyze,
                    T("Log.Event.AnalyzeBaseline", score.Total, T(score.Verdict)));
            }
            else
            {
                ScoreAfter = score;
                var delta = score.Total - ScoreBefore.Total;
                var sign = delta >= 0 ? "+" : "";
                StatusMessage = T("Status.AnalysisFinal", score.Total, T(score.Verdict), sign, delta);
                var level = delta >= 0 ? AppLogLevel.Success : AppLogLevel.Warning;
                await _appLog.WriteAsync(level, AppLogCategory.Analyze,
                    T("Log.Event.AnalyzeFinal", score.Total, T(score.Verdict), sign, delta));
            }
        }
        catch (Exception ex)
        {
            StatusMessage = T("Status.AnalysisError", ex.Message);
            _logger.LogError(ex, "Fallo el analisis de rendimiento");
            await _appLog.ErrorAsync(AppLogCategory.Analyze,
                T("Log.Event.AnalyzeFailed", ex.Message), ex.ToString());
        }
        finally
        {
            IsAnalyzing = false;
            AnalyzeCommand.NotifyCanExecuteChanged();
            ResetScoresCommand.NotifyCanExecuteChanged();
            RunCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanResetScores))]
    private void ResetScores()
    {
        ScoreBefore = null;
        ScoreAfter = null;
        IsBaselineFromFile = false;
        StatusMessage = T("Status.ScoresReset");
        AnalyzeCommand.NotifyCanExecuteChanged();
        ResetScoresCommand.NotifyCanExecuteChanged();
        // Borra también el archivo persistido.
        _ = _baseline.ClearAsync();
    }

    private bool CanAnalyze() => !IsAnalyzing && !IsRunning;
    private bool CanResetScores() => !IsAnalyzing && (ScoreBefore is not null || ScoreAfter is not null);

    partial void OnScoreBeforeChanged(PerformanceScore? value)
    {
        ResetScoresCommand.NotifyCanExecuteChanged();
        if (value is not null && !IsBaselineFromFile)
        {
            // Auto-guardar en disco cuando el usuario mide un nuevo baseline.
            _ = _baseline.SaveAsync(value);
        }
    }

    partial void OnScoreAfterChanged(PerformanceScore? value)
        => ResetScoresCommand.NotifyCanExecuteChanged();

    // Limpieza rápida

    [RelayCommand(CanExecute = nameof(CanQuickClean))]
    private async Task QuickCleanAsync()
    {
        // Diálogo de confirmación. Usamos Wpf.Ui MessageBox para mantener
        // la coherencia visual con el resto del Fluent design.
        var confirm = new Wpf.Ui.Controls.MessageBox
        {
            Title             = T("QuickClean.ConfirmTitle"),
            Content           = T("QuickClean.ConfirmMessage"),
            PrimaryButtonText = T("Common.OK"),
            CloseButtonText   = T("Common.Cancel"),
        };
        var result = await confirm.ShowDialogAsync();
        if (result != Wpf.Ui.Controls.MessageBoxResult.Primary) return;

        IsQuickCleaning = true;
        QuickCleanCommand.NotifyCanExecuteChanged();
        RunCommand.NotifyCanExecuteChanged();
        AnalyzeCommand.NotifyCanExecuteChanged();
        StatusMessage = T("Status.QuickCleaning");

        try
        {
            var clean = await _quickClean.CleanAsync();
            var mb = Math.Round(clean.BytesFreed / 1_048_576.0, 1);
            StatusMessage = T("Status.QuickCleanDone", mb, clean.FilesDeleted);
            await _appLog.SuccessAsync(AppLogCategory.QuickClean,
                T("Log.Event.QuickCleanFinished", mb, clean.FilesDeleted));
        }
        catch (Exception ex)
        {
            StatusMessage = T("Status.QuickCleanError", ex.Message);
            _logger.LogError(ex, "Fallo en limpieza rápida");
            await _appLog.ErrorAsync(AppLogCategory.QuickClean,
                T("Log.Event.QuickCleanFailed", ex.Message), ex.ToString());
        }
        finally
        {
            IsQuickCleaning = false;
            QuickCleanCommand.NotifyCanExecuteChanged();
            RunCommand.NotifyCanExecuteChanged();
            AnalyzeCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanQuickClean() => !IsRunning && !IsAnalyzing && !IsQuickCleaning;

    // Actualizar aplicaciones con winget

    [RelayCommand(CanExecute = nameof(CanWingetUpgrade))]
    private async Task WingetUpgradeAsync()
    {
        if (!_winget.IsAvailable)
        {
            var unavail = new Wpf.Ui.Controls.MessageBox
            {
                Title             = "winget",
                Content           = T("Winget.NotAvailable"),
                CloseButtonText   = T("Common.Close"),
            };
            await unavail.ShowDialogAsync();
            return;
        }

        var confirm = new Wpf.Ui.Controls.MessageBox
        {
            Title             = T("Winget.ConfirmTitle"),
            Content           = T("Winget.ConfirmMessage"),
            PrimaryButtonText = T("Common.OK"),
            CloseButtonText   = T("Common.Cancel"),
        };
        if (await confirm.ShowDialogAsync() != Wpf.Ui.Controls.MessageBoxResult.Primary)
            return;

        IsUpdatingApps = true;
        WingetUpgradeCommand.NotifyCanExecuteChanged();
        StatusMessage = T("Status.WingetRunning");
        await _appLog.InfoAsync(AppLogCategory.Run, T("Log.Event.WingetStarted"));

        // Navega al registro para que el usuario vea el progreso en vivo.
        CurrentSection = AppSection.Log;

        try
        {
            var progress = new Progress<string>(line =>
            {
                if (!string.IsNullOrWhiteSpace(line))
                    _ = _appLog.InfoAsync(AppLogCategory.Run, line);
            });

            var result = await _winget.UpgradeAllAsync(progress);

            StatusMessage = T("Status.WingetDone", result.PackagesUpdated);
            await _appLog.SuccessAsync(AppLogCategory.Run,
                T("Log.Event.WingetFinished", result.PackagesUpdated));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo en winget upgrade");
            StatusMessage = T("Status.WingetError", ex.Message);
            await _appLog.ErrorAsync(AppLogCategory.Run,
                T("Log.Event.WingetFailed", ex.Message), ex.ToString());
        }
        finally
        {
            IsUpdatingApps = false;
            WingetUpgradeCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanWingetUpgrade() => !IsRunning && !IsAnalyzing && !IsUpdatingApps && !IsQuickCleaning;

    // Comprobación de seguridad

    [ObservableProperty]
    private bool isSecurityChecking;

    [ObservableProperty]
    private SecurityCheckKind selectedSecurityCheck = SecurityCheckKind.DefenderQuick;

    /// <summary>Opciones del ComboBox de selección de herramienta.</summary>
    public IReadOnlyList<SecurityCheckKind> AvailableSecurityChecks { get; } =
    [
        SecurityCheckKind.DefenderQuick,
        SecurityCheckKind.Sfc,
        SecurityCheckKind.Dism,
        SecurityCheckKind.Mrt,
    ];

    [RelayCommand(CanExecute = nameof(CanRunSecurityCheck))]
    private async Task RunSecurityCheckAsync()
    {
        var kind = SelectedSecurityCheck;
        if (!_security.IsAvailable(kind))
        {
            var unavail = new Wpf.Ui.Controls.MessageBox
            {
                Title           = T("Security.NotAvailable.Title"),
                Content         = T("Security.NotAvailable.Message", kind),
                CloseButtonText = T("Common.Close"),
            };
            await unavail.ShowDialogAsync();
            return;
        }

        var confirm = new Wpf.Ui.Controls.MessageBox
        {
            Title             = T("Security.Confirm.Title"),
            Content           = T($"Security.Confirm.{kind}"),
            PrimaryButtonText = T("Common.OK"),
            CloseButtonText   = T("Common.Cancel"),
        };
        if (await confirm.ShowDialogAsync() != Wpf.Ui.Controls.MessageBoxResult.Primary)
            return;

        IsSecurityChecking = true;
        RunSecurityCheckCommand.NotifyCanExecuteChanged();
        StatusMessage = T("Status.SecurityRunning", kind);
        await _appLog.InfoAsync(AppLogCategory.Run, T("Log.Event.SecurityStarted", kind));
        CurrentSection = AppSection.Log;

        try
        {
            var progress = new Progress<string>(line =>
            {
                if (!string.IsNullOrWhiteSpace(line))
                    _ = _appLog.InfoAsync(AppLogCategory.Run, line);
            });

            var result = await _security.RunAsync(kind, progress);

            if (result.Succeeded)
            {
                StatusMessage = T("Status.SecurityDone", kind);
                await _appLog.SuccessAsync(AppLogCategory.Run,
                    T("Log.Event.SecurityFinished", kind, result.ExitCode));
            }
            else
            {
                StatusMessage = T("Status.SecurityError", kind, result.ExitCode);
                await _appLog.WarningAsync(AppLogCategory.Run,
                    T("Log.Event.SecurityFinishedWarn", kind, result.ExitCode));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo en security check {Kind}", kind);
            StatusMessage = T("Status.SecurityError", kind, ex.Message);
            await _appLog.ErrorAsync(AppLogCategory.Run,
                T("Log.Event.SecurityFailed", kind, ex.Message), ex.ToString());
        }
        finally
        {
            IsSecurityChecking = false;
            RunSecurityCheckCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanRunSecurityCheck() => !IsRunning && !IsAnalyzing && !IsSecurityChecking;

    // Revertir cambios

    public ObservableCollection<ModuleChangeRecord> PendingChanges { get; } = new();

    [ObservableProperty]
    private bool isRefreshingChanges;

    [ObservableProperty]
    private bool isReverting;

    [ObservableProperty]
    private int pendingChangesCount;

    private bool _changesBadgeDismissed;

    /// <summary>True cuando hay cambios en la lista (controla visibilidad de tabla y botón Revertir todo).</summary>
    public bool HasChangesInList => PendingChanges.Count > 0;

    /// <summary>True cuando hay cambios pendientes Y el badge no ha sido descartado (controla badge sidebar).</summary>
    public bool HasPendingChanges => PendingChangesCount > 0 && !_changesBadgeDismissed;

    partial void OnPendingChangesCountChanged(int value)
    {
        if (value > 0) _changesBadgeDismissed = false;
        OnPropertyChanged(nameof(HasPendingChanges));
        OnPropertyChanged(nameof(HasChangesInList));
    }

    [RelayCommand]
    private void DismissChangesBadge()
    {
        _changesBadgeDismissed = true;
        OnPropertyChanged(nameof(HasPendingChanges));
        // HasChangesInList no cambia: la lista sigue visible
    }

    [RelayCommand]
    private async Task RefreshChangesAsync()
    {
        IsRefreshingChanges = true;
        try
        {
            var list = await _changes.GetPendingChangesAsync();
            PendingChanges.Clear();
            foreach (var c in list) PendingChanges.Add(c);
            PendingChangesCount = list.Count;
            OnPropertyChanged(nameof(HasChangesInList));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo cargar la lista de cambios pendientes");
        }
        finally
        {
            IsRefreshingChanges = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRevert))]
    private async Task RevertChangeAsync(ModuleChangeRecord? record)
    {
        if (record is null) return;

        var confirm = new Wpf.Ui.Controls.MessageBox
        {
            Title             = T("Restore.Revert.ConfirmTitle"),
            Content           = T("Restore.Revert.ConfirmOne", record.ModuleName),
            PrimaryButtonText = T("Common.OK"),
            CloseButtonText   = T("Common.Cancel"),
        };
        if (await confirm.ShowDialogAsync() != Wpf.Ui.Controls.MessageBoxResult.Primary)
            return;

        IsReverting = true;
        try
        {
            var ok = await _changes.RevertChangeAsync(record.Id);
            await _appLog.WriteAsync(
                ok ? AppLogLevel.Success : AppLogLevel.Warning,
                AppLogCategory.Run,
                T(ok ? "Log.Event.RevertOk" : "Log.Event.RevertFailed",
                  record.ModuleName, record.Target));
            await RefreshChangesAsync();
            await SyncAppliedStateAfterRevertAsync();
        }
        finally
        {
            IsReverting = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRevert))]
    private async Task RevertAllChangesAsync()
    {
        if (PendingChangesCount == 0) return;

        var confirm = new Wpf.Ui.Controls.MessageBox
        {
            Title             = T("Restore.Revert.ConfirmTitle"),
            Content           = T("Restore.Revert.ConfirmAll", PendingChangesCount),
            PrimaryButtonText = T("Common.OK"),
            CloseButtonText   = T("Common.Cancel"),
        };
        if (await confirm.ShowDialogAsync() != Wpf.Ui.Controls.MessageBoxResult.Primary)
            return;

        IsReverting = true;
        try
        {
            var result = await _changes.RevertAllPendingAsync();
            await _appLog.InfoAsync(AppLogCategory.Run,
                T("Log.Event.RevertAllDone", result.Reverted, result.Failed, result.Total));
            StatusMessage = T("Status.RevertDone", result.Reverted, result.Total);
            await RefreshChangesAsync();
            await SyncAppliedStateAfterRevertAsync();
        }
        finally
        {
            IsReverting = false;
        }
    }

    private bool CanRevert() => !IsReverting;

    // Reporte de bugs

    [RelayCommand]
    private async Task ReportBugAsync()
    {
        try
        {
            await _bugReport.OpenBugReportAsync();
            await _appLog.InfoAsync(AppLogCategory.App, T("Log.Event.BugReportOpened"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo abrir el cliente de correo");
            var err = new Wpf.Ui.Controls.MessageBox
            {
                Title           = T("BugReport.Error.Title"),
                Content         = T("BugReport.Error.Message", ex.Message),
                CloseButtonText = T("Common.Close"),
            };
            await err.ShowDialogAsync();
        }
    }

    [RelayCommand]
    private async Task SuggestFeatureAsync()
    {
        try
        {
            await _bugReport.OpenSuggestionAsync();
            await _appLog.InfoAsync(AppLogCategory.App, T("Log.Event.SuggestionOpened"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo abrir el cliente de correo para sugerencia");
            var err = new Wpf.Ui.Controls.MessageBox
            {
                Title           = T("BugReport.Error.Title"),
                Content         = T("BugReport.Error.Message", ex.Message),
                CloseButtonText = T("Common.Close"),
            };
            await err.ShowDialogAsync();
        }
    }

    // Mantenimiento de la propia app

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)        return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024):F1} MB";
    }

    [RelayCommand]
    private async Task CleanAppDataAsync()
    {
        try
        {
            var (folders, bytes) = await _selfCleanup.CleanAsync();
            var msg = folders == 0
                ? T("Settings.SelfCleanup.NothingToClean")
                : T("Settings.SelfCleanup.Result", folders, FormatBytes(bytes));
            await _appLog.InfoAsync(AppLogCategory.App, msg);
            var box = new Wpf.Ui.Controls.MessageBox
            {
                Title           = T("Settings.SelfCleanup.Title"),
                Content         = msg,
                CloseButtonText = T("Common.Close"),
            };
            await box.ShowDialogAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al limpiar rastros de DexSuite");
        }
    }

    // Optimización de videojuegos

    /// <summary>
    /// Abre la ventana modal de selección de juegos. Cada juego viene del
    /// catálogo en <see cref="IGameOptimizationService.AvailableGames"/> y al
    /// pulsar Optimizar dentro del tile se descarga + ejecuta el .ps1
    /// correspondiente del repo SrDexterGF/Game_Configs.
    /// </summary>
    private bool CanOpenGameSelector() =>
        UserTierEnum is ModuleTier.Pro;

    [RelayCommand(CanExecute = nameof(CanOpenGameSelector))]
    private async Task OpenGameSelectorAsync()
    {
        try
        {
            {
                var disclaimer = new Wpf.Ui.Controls.MessageBox
                {
                    Title             = T("GamingDisclaimer.Title"),
                    Content           = new System.Windows.Controls.TextBlock
                    {
                        Text         = T("GamingDisclaimer.Body"),
                        TextWrapping = System.Windows.TextWrapping.Wrap,
                    },
                    PrimaryButtonText = T("GamingDisclaimer.Confirm"),
                    CloseButtonText   = T("GamingDisclaimer.Cancel"),
                };
                var result = await disclaimer.ShowDialogAsync();
                if (result != Wpf.Ui.Controls.MessageBoxResult.Primary) return;
            }

            var window = (Window)_services.GetService(typeof(GameSelectorWindow))!;
            window.Owner = Application.Current?.MainWindow;
            window.ShowDialog();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo abrir la ventana de selección de juegos");
            StatusMessage = T("Gaming.OpenError", ex.Message);
        }
    }

    // Diálogos previos al run

    /// <summary>
    /// Pregunta al usuario si quiere crear un punto de restauración antes de
    /// ejecutar cualquier optimización. Esta versión SIEMPRE muestra
    /// el diálogo — el toggle de Ajustes <see cref="CreateRestorePointBeforeRun"/>
    /// solo influye en cuál de las dos opciones está pre-seleccionada por
    /// defecto (Sí si está activado, No si está desactivado).
    ///
    /// Devuelve:
    ///   - true  → continuar y crear punto.
    ///   - false → continuar sin crear punto.
    ///   - null  → abortar el run.
    /// </summary>
    private async Task<bool?> AskRestorePointBeforeRunAsync()
    {
        var dialog = new Wpf.Ui.Controls.MessageBox
        {
            Title               = T("RestorePoint.ConfirmTitle"),
            Content             = T("RestorePoint.ConfirmMessage"),
            PrimaryButtonText   = T("RestorePoint.ConfirmYes"),
            SecondaryButtonText = T("RestorePoint.ConfirmNo"),
            CloseButtonText     = T("Common.Cancel"),
        };
        var res = await dialog.ShowDialogAsync();
        return res switch
        {
            Wpf.Ui.Controls.MessageBoxResult.Primary   => true,
            Wpf.Ui.Controls.MessageBoxResult.Secondary => false,
            _ => (bool?)null, // cancel / close
        };
    }

    /// <summary>
    /// Muestra una notificación nativa de Windows al terminar el run, si el
    /// usuario tiene activada la opción <see cref="NotifyOnFinish"/>. Cualquier
    /// fallo se loguea silencioso — la notificación es accesoria.
    /// </summary>
    private async Task TryShowFinishNotificationAsync(int moduleCount, bool hadErrors)
    {
        if (!NotifyOnFinish) return;
        // Las toast notifications nativas requieren Win10 build 19041+. Si el
        // SO es más antiguo, omitimos silenciosamente — la notificación es
        // accesoria.
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041)) return;
        try
        {
            var title = T("Notification.RunFinished.Title");
            var body  = hadErrors
                ? T("Notification.RunFinished.WithErrors", moduleCount)
                : T("Notification.RunFinished.Body", moduleCount);
            // CA1416: el guard de versión arriba (IsWindowsVersionAtLeast(10,0,19041))
            // garantiza la plataforma, pero el analizador no rastrea inter-métodos.
#pragma warning disable CA1416
            await Task.Run(() => ToastNotificationService.Show(title, body));
#pragma warning restore CA1416
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo emitir la notificación toast");
        }
    }

    // Constructor

    public MainViewModel(
        IModuleCatalog catalog,
        INativeModuleRunner runner,
        IPerformanceAnalyzer analyzer,
        IUpdateService updateService,
        ILocalizationService loc,
        IQuickCleanService quickClean,
        ISystemInfoService systemInfo,
        IAppLogService appLog,
        IPerformanceBaselineService baseline,
        IRestorePointService restorePoint,
        IThemeService themeService,
        ISettingsService settingsService,
        IServiceProvider services,
        IWingetService winget,
        ISecurityCheckService security,
        IChangeTrackingService changes,
        IModuleStateService moduleState,
        IBugReportService bugReport,
        IAppSelfCleanupService selfCleanup,
        ILicenseService license,
        ILogger<MainViewModel> logger)
    {
        _runner = runner;
        _analyzer = analyzer;
        _updateService = updateService;
        _loc = loc;
        _quickClean = quickClean;
        _systemInfo = systemInfo;
        _appLog = appLog;
        _baseline = baseline;
        _restorePoint = restorePoint;
        _themeService = themeService;
        _settingsService = settingsService;
        _services = services;
        _winget = winget;
        _security = security;
        _changes = changes;
        _moduleState = moduleState;
        _bugReport = bugReport;
        _selfCleanup = selfCleanup;
        _license = license;
        _logger = logger;

        // HWID inicial + tier real desde el servicio de licencias.
        // Se obtiene ANTES de hidratar settings para que UserTier no quede
        // fijado al persistido (que ya no es la fuente de verdad).
        HardwareId = _license.GetHardwareId();

        // Hidrata valores persistidos antes de enganchar la lógica de save.
        // El flag _settingsHydrated permanece en false durante este bloque
        // para que cada SetXxx no dispare ScheduleSave una y otra vez.
        var persisted = _settingsService.Load();
        AutoSelectRecommended       = persisted.AutoSelectRecommended;
        JumpToLogOnRun              = persisted.JumpToLogOnRun;
        IsAdvancedModuleView        = persisted.IsAdvancedModuleView;
        WarnBeforeNonReversible     = persisted.WarnBeforeNonReversible;
        CreateRestorePointBeforeRun = persisted.CreateRestorePointBeforeRun;
        NotifyOnFinish              = persisted.NotifyOnFinish;
        // El tier ya NO se hidrata desde settings: viene del servicio de licencias
        // (firma RSA + HWID). El campo persistido se ignora en lectura para evitar
        // que un settings.json modificado a mano otorgue Pro sin clave válida.
        UserTier                    = TierToString(_license.CurrentTier);
        AutoUpdateEnabled           = persisted.AutoUpdateEnabled;
        MinimizeToTray              = persisted.MinimizeToTray;
        ShowGamingDisclaimer        = persisted.ShowGamingDisclaimer;
        UpdateChannel               = persisted.UpdateChannel;
        if (!string.IsNullOrWhiteSpace(persisted.Language))
            _loc.CurrentLanguage = persisted.Language;
        _settingsHydrated = true;

        // Reacciona a cambios del tier emitidos por el watchdog o por activaciones.
        _license.TierChanged += (_, tier) =>
        {
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                UserTier = TierToString(tier);
                LicenseStatusMessage = T($"License.Status.{tier}");
            });
        };
        LicenseStatusMessage = T($"License.Status.{_license.CurrentTier}");

        // Sincroniza el tema actual al que cargó App.xaml.cs antes de mostrar la
        // ventana. Si en runtime se cambia, ThemeChanged actualizará la UI.
        CurrentTheme = _themeService.CurrentTheme;
        foreach (var desc in _themeService.AvailableThemes)
        {
            ThemeItems.Add(new ThemeItemViewModel(
                desc,
                isActive:   desc.Theme == CurrentTheme,
                isUnlocked: IsThemeUnlocked(desc.MinTier)));
        }
        // Tarjeta "Coming Soon" al final del listado normal: no seleccionable.
        ThemeItems.Add(new ThemeItemViewModel(
            new ThemeDescriptor(AppTheme.Default, "Settings.Theme.ComingSoon", "Settings.Theme.ComingSoon",
                Color.FromRgb(0x20, 0x20, 0x28), Color.FromRgb(0x40, 0x40, 0x4A), Color.FromRgb(0x60, 0x60, 0x6A), "Pro"),
            isActive: false, isUnlocked: false, isComingSoon: true));

        // Temas de videojuegos (Expander "Temas 😉").
        foreach (var desc in _themeService.GameThemes)
        {
            GameThemeItems.Add(new ThemeItemViewModel(
                desc,
                isActive:   desc.Theme == CurrentTheme,
                isUnlocked: IsThemeUnlocked(desc.MinTier)));
        }
        _themeService.ThemeChanged += (_, theme) =>
        {
            // El setter dispara OnCurrentThemeChanged → RefreshThemeItems.
            CurrentTheme = theme;
        };

        // Mensaje inicial localizado. Cuando el usuario cambia el idioma,
        // re-emitimos el mensaje de "Listo" si seguimos en ese estado.
        StatusMessage = T("Status.Ready");
        _loc.LanguageChanged += (_, _) =>
        {
            // Re-emitimos propiedades cuyo texto depende del idioma:
            OnPropertyChanged(nameof(CurrentLanguage));
            OnPropertyChanged(nameof(LastUpdateCheckLabel));
            OnPropertyChanged(nameof(AvailableUpdateVersionLabel));
            OnPropertyChanged(nameof(ChangelogTitle));
            // El idioma forma parte de los ajustes persistidos.
            PersistSettings();
            // Registramos el cambio en el historial interno.
            _ = _appLog.InfoAsync(AppLogCategory.Language,
                T("Log.Event.LanguageChanged", CurrentLanguage));
            // El StatusMessage es un mensaje puntual; no lo refrescamos.
            // Las próximas asignaciones ya saldrán en el idioma nuevo.
        };

        // Suscripción al historial para refresco en vivo de la vista.
        _appLog.EntryAdded += OnAppLogEntryAdded;

        // Notifica HasAppLogEntries / IsAppLogEmpty automáticamente cuando cambia
        // la colección, sin necesidad de llamadas manuales dispersas por el código.
        AppLogEntries.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasAppLogEntries));
            OnPropertyChanged(nameof(IsAppLogEmpty));
        };

        LogsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DexSuite", "logs");

        foreach (var m in catalog.GetAll())
        {
            var vm = new ModuleItemViewModel(m, initiallyEnabled: AutoSelectRecommended && IsRecommendedForTier(m.Id, UserTierEnum), _loc);
            Modules.Add(vm);
            switch (m.Tier)
            {
                case ModuleTier.Free:
                    FreeModules.Add(vm);
                    break;
                case ModuleTier.Advanced:
                    AdvancedModules.Add(vm);
                    break;
                case ModuleTier.Pro:
                    // Los módulos Extras (videojuegos) van a su propia sección visual.
                    if (m.Category == ModuleCategory.Extras)
                        ProExtraModules.Add(vm);
                    else
                        ProModules.Add(vm);
                    break;
            }
        }

        // Estado inicial de bloqueo según el tier configurado.
        UpdateModuleLockStates();

        // Rehidrata el estado "aplicado" persistido (barra → tick entre sesiones).
        _ = HydrateModuleAppliedStatesAsync();

        // Carga el baseline guardado en disco (fire-and-forget — no bloquea la UI).
        _ = LoadPersistedBaselineAsync();

        // Comprueba actualizaciones al arrancar (fire-and-forget).
        // Si hay update disponible, HasAvailableUpdate → IsUpdateAvailable pone la flecha verde.
        _ = CheckForUpdatesCommand.ExecuteAsync(null);

        // Carga el contador de cambios pendientes (fire-and-forget).
        _ = LoadPendingChangesCountAsync();

        // Evento de arranque en el historial interno (fire-and-forget).
        _ = _appLog.InfoAsync(AppLogCategory.App,
            T("Log.Event.AppStarted", _updateService.CurrentVersion));
    }

    /// <summary>
    /// Carga desde SQLite qué módulos están marcados como aplicados y lo refleja
    /// en la UI (indicador en estado "tick"). Fire-and-forget al arrancar.
    /// </summary>
    private async Task HydrateModuleAppliedStatesAsync()
    {
        try
        {
            var applied = await _moduleState.GetAppliedModuleIdsAsync().ConfigureAwait(false);
            if (applied.Count == 0) return;
            var set = new HashSet<int>(applied);
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                foreach (var m in Modules)
                    if (set.Contains(m.Id)) m.IsApplied = true;
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo hidratar el estado aplicado de los módulos");
        }
    }

    /// <summary>
    /// Tras revertir, recalcula qué módulos siguen "aplicados". Un módulo deja de
    /// estar aplicado cuando TODOS sus cambios registrados han sido revertidos.
    /// Los módulos de limpieza (sin cambios registrados) no se tocan.
    /// </summary>
    private async Task SyncAppliedStateAfterRevertAsync()
    {
        try
        {
            var all = await _changes.GetAllChangesAsync().ConfigureAwait(false);

            // moduleId(int) → tiene algún cambio aún pendiente
            var stillPending = new HashSet<int>();
            var everTracked  = new HashSet<int>();
            foreach (var c in all)
            {
                if (!int.TryParse(c.ModuleId, out var mid)) continue;
                everTracked.Add(mid);
                if (!c.IsReverted) stillPending.Add(mid);
            }

            // Módulos cuyos cambios ya están todos revertidos → ya no aplicados.
            var noLongerApplied = everTracked.Where(id => !stillPending.Contains(id)).ToList();
            if (noLongerApplied.Count == 0) return;

            foreach (var id in noLongerApplied)
                await _moduleState.SetAppliedAsync(id, false).ConfigureAwait(false);

            var set = new HashSet<int>(noLongerApplied);
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                foreach (var m in Modules)
                    if (set.Contains(m.Id)) m.IsApplied = false;
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo sincronizar el estado aplicado tras revertir");
        }
    }

    private async Task LoadPendingChangesCountAsync()
    {
        try { PendingChangesCount = await _changes.CountPendingAsync(); }
        catch (Exception ex) { _logger.LogWarning(ex, "No se pudo cargar el contador de cambios pendientes"); }
    }

    private async Task LoadPersistedBaselineAsync()
    {
        try
        {
            var saved = await _baseline.LoadAsync().ConfigureAwait(false);
            if (saved is null) return;

            // Dispatch al hilo de UI para modificar propiedades observables.
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                // Marcamos como "desde archivo" ANTES de asignar ScoreBefore
                // para que OnScoreBeforeChanged no reescriba el archivo.
                IsBaselineFromFile = true;
                ScoreBefore = saved;
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo cargar el baseline persistido");
        }
    }

    // Ejecutar / Cancelar

    /// <summary>Módulo actualmente en estado Running, null si ninguno.</summary>
    private ModuleItemViewModel? _currentRunningModule;

    /// <summary>Milisegundos mínimos que el círculo de carga permanece visible por módulo.</summary>
    private const int MinSpinnerVisibleMs = 600;

    /// <summary>Marca temporal de cuándo empezó a mostrarse el módulo actual (para el dwell del spinner).</summary>
    private DateTime _currentModuleVisibleSince;

    /// <summary>Acumulado de errores vistos dentro del bloque actual.</summary>
    private string? _currentRunErrorMessage;

    /// <summary>
    /// Reinicia el estado de ejecución de todos los módulos a Idle. Se llama
    /// al inicio de cada run para que la UI muestre el estado limpio.
    /// </summary>
    private void ResetModuleRunStates()
    {
        foreach (var m in Modules)
        {
            m.RunStatus = ModuleRunStatus.Idle;
            m.LastError = null;
        }
    }

    /// <summary>
    /// Convierte un ModuleProgress a una línea de texto para el log interno.
    /// </summary>
    private static string FormatProgressLine(ModuleProgress p) => p.Kind switch
    {
        ModuleProgressKind.Header    => $"┌─ M{p.ModuleId:00} ── {p.Message}",
        ModuleProgressKind.Step      => $"  → {p.Message}",
        ModuleProgressKind.Ok        => $"  [OK] {p.Message}",
        ModuleProgressKind.Warn      => $"  [WARN] {p.Message}",
        ModuleProgressKind.Error     => $"  [ERROR] {p.Message}",
        ModuleProgressKind.Info      => $"     {p.Message}",
        ModuleProgressKind.Heartbeat => $"  [...] {p.Message}",
        ModuleProgressKind.Done      => $"└─ {p.Message}",
        _ => p.Message,
    };

    /// <summary>
    /// Procesa un evento estructurado del runner nativo y actualiza el estado
    /// de los módulos correspondientes.
    /// </summary>
    private void ProcessModuleProgress(ModuleProgress p)
    {
        switch (p.Kind)
        {
            case ModuleProgressKind.Header:
                // Cerramos el módulo previo (defensivo) y abrimos el nuevo.
                FinishCurrentModule();
                var match = Modules.FirstOrDefault(m => m.Id == p.ModuleId);
                if (match is not null)
                {
                    match.RunStatus = ModuleRunStatus.Running;
                    _currentRunningModule = match;
                    _currentRunErrorMessage = null;
                    _ = _appLog.InfoAsync(AppLogCategory.Run,
                        T("Log.Event.ModuleStarted", match.Name));
                }
                break;

            case ModuleProgressKind.Error:
                if (_currentRunningModule is not null)
                {
                    _currentRunErrorMessage = string.IsNullOrEmpty(_currentRunErrorMessage)
                        ? p.Message
                        : $"{_currentRunErrorMessage}; {p.Message}";
                }
                break;

            case ModuleProgressKind.Done:
                FinishCurrentModule();
                break;
        }
    }

    /// <summary>
    /// Cierra el módulo en ejecución actual. Si vio errores → Error;
    /// si no → Success. Limpia el slot _currentRunningModule.
    /// </summary>
    private void FinishCurrentModule()
    {
        if (_currentRunningModule is null) return;
        var m = _currentRunningModule;

        if (_currentRunErrorMessage is not null)
        {
            m.RunStatus = ModuleRunStatus.Error;
            m.LastError = _currentRunErrorMessage;
            _ = _appLog.ErrorAsync(AppLogCategory.Run,
                T("Log.Event.ModuleError", m.Name, _currentRunErrorMessage));
        }
        else
        {
            m.RunStatus = ModuleRunStatus.Success;
            // Estado "aplicado" persistente: la barra pasa a tick y sobrevive
            // al reinicio. Fire-and-forget; el indicador ya refleja el cambio.
            m.IsApplied = true;
            _ = _moduleState.SetAppliedAsync(m.Id, true);
            _ = _appLog.SuccessAsync(AppLogCategory.Run,
                T("Log.Event.ModuleCompleted", m.Name));
        }

        _currentRunningModule = null;
        _currentRunErrorMessage = null;
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task RunAsync()
    {
        var selected = Modules.Where(m => m.IsEnabled).Select(m => m.Id).OrderBy(id => id).ToList();
        if (selected.Count == 0)
        {
            StatusMessage = T("Status.SelectModulesFirst");
            return;
        }
        _runModuleTotal = selected.Count;
        _runModuleIndex = 0;

        // Diálogo previo: ¿crear punto de restauración?
        var rpDecision = await AskRestorePointBeforeRunAsync();
        if (rpDecision is null) return; // usuario canceló
        if (rpDecision.Value) await TryAutoRestorePointAsync(selected.Count);

        IsRunning = true;
        OutputLog = string.Empty;
        lock (_bufferLock) _pendingBuffer.Clear();
        ResetModuleRunStates();
        StatusMessage = T("Status.Executing", selected.Count);
        if (JumpToLogOnRun)
            CurrentSection = AppSection.Log;

        _runCts = new CancellationTokenSource();
        var ct = _runCts.Token;
        RunCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        AnalyzeCommand.NotifyCanExecuteChanged();

        using var flushCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var flushTask = Task.Run(async () =>
        {
            try
            {
                while (!flushCts.IsCancellationRequested)
                {
                    await Task.Delay(100, flushCts.Token);
                    FlushBuffer();
                }
            }
            catch (OperationCanceledException) { /* esperado al terminar */ }
        }, flushCts.Token);

        try
        {
            _logger.LogInformation("Lanzando ejecución nativa con módulos: {Modules}", string.Join(",", selected));
            var modulesStr = string.Join(", ", Modules.Where(m => m.IsEnabled).Select(m => m.Name));
            await _appLog.InfoAsync(AppLogCategory.Run,
                T("Log.Event.RunStarted", selected.Count),
                modulesStr);

            // En vista avanzada, construimos el mapa moduleId → sub-opciones marcadas.
            // En vista simple pasamos null (cada módulo ejecuta todo).
            var subMap = BuildSubOptionsMap(selected);

            await foreach (var progress in _runner.RunAsync(selected, subMap, ct).WithCancellation(ct))
            {
                var line = FormatProgressLine(progress);
                lock (_bufferLock) _pendingBuffer.AppendLine(line);

                // Garantiza que el círculo de carga (estado Applying) sea visible
                // un mínimo de tiempo. Los módulos de registro terminan en pocos
                // milisegundos; sin esto, Header y Done llegan casi a la vez y WPF
                // nunca pinta un fotograma con el spinner (barra → tick directo).
                if (progress.Kind == ModuleProgressKind.Header)
                {
                    _runModuleIndex++;
                    var idx = _runModuleIndex;
                    var tot = _runModuleTotal;
                    // Prefix in live log so the user can follow progress there too.
                    lock (_bufferLock) _pendingBuffer.AppendLine($"── Módulo {idx} de {tot} ──");
                    // Update the bottom-bar progress label and percentage on the UI thread.
                    Application.Current?.Dispatcher.BeginInvoke(() =>
                    {
                        RunProgressText    = $"Módulo {idx} de {tot}";
                        RunProgressPercent = tot > 0 ? idx / (double)tot * 100.0 : 0.0;
                    });
                    _currentModuleVisibleSince = DateTime.UtcNow;
                }
                else if (progress.Kind == ModuleProgressKind.Done)
                {
                    var elapsedMs = (DateTime.UtcNow - _currentModuleVisibleSince).TotalMilliseconds;
                    var remaining = MinSpinnerVisibleMs - elapsedMs;
                    if (remaining > 0)
                        await Task.Delay((int)remaining, ct);
                }

                // El parser estructurado actualiza estados de módulo en el hilo de UI.
                var captured = progress;
                Application.Current?.Dispatcher.BeginInvoke(() => ProcessModuleProgress(captured));
            }

            // Cierra el último módulo en estado Running cuando termina la ejecución.
            Application.Current?.Dispatcher.Invoke(FinishCurrentModule);

            StatusMessage = T("Status.ExecutionDone");
            await _appLog.SuccessAsync(AppLogCategory.Run, T("Log.Event.RunFinished", selected.Count));
            // Notificación toast al terminar.
            await TryShowFinishNotificationAsync(selected.Count, hadErrors: false);
        }
        catch (OperationCanceledException)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                if (_currentRunningModule is not null)
                {
                    _currentRunningModule.RunStatus = ModuleRunStatus.Idle;
                    _currentRunningModule = null;
                }
            });
            StatusMessage = T("Status.ExecutionCancelled");
            await _appLog.WarningAsync(AppLogCategory.Run, T("Log.Event.RunCancelled"));
        }
        catch (Exception ex)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                if (_currentRunningModule is not null)
                {
                    _currentRunningModule.RunStatus = ModuleRunStatus.Error;
                    _currentRunningModule.LastError = ex.Message;
                    _currentRunningModule = null;
                }
            });
            StatusMessage = T("Status.ExecutionError", ex.Message);
            _logger.LogError(ex, "Fallo al ejecutar los módulos");
            await _appLog.ErrorAsync(AppLogCategory.Run, T("Log.Event.RunFailed", ex.Message), ex.ToString());
            await TryShowFinishNotificationAsync(selected.Count, hadErrors: true);
        }
        finally
        {
            flushCts.Cancel();
            try { await flushTask; } catch { /* swallowed */ }
            FlushBuffer();

            RunProgressText    = string.Empty;
            RunProgressPercent = 0.0;
            IsRunning = false;
            _runCts?.Dispose();
            _runCts = null;
            RunCommand.NotifyCanExecuteChanged();
            CancelCommand.NotifyCanExecuteChanged();
            AnalyzeCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        try { _runCts?.Cancel(); } catch { /* ya disposed */ }
        StatusMessage = T("Status.Cancelling");
    }

    private bool CanRun() => !IsRunning && !IsAnalyzing;
    private bool CanCancel() => IsRunning;

    // Helpers internos

    private void FlushBuffer()
    {
        string toAppend;
        lock (_bufferLock)
        {
            if (_pendingBuffer.Length == 0) return;
            toAppend = _pendingBuffer.ToString();
            _pendingBuffer.Clear();
        }

        var combined = OutputLog + toAppend;
        if (combined.Length > MaxLogChars)
        {
            var cut = combined.Length - MaxLogChars;
            var nextNewline = combined.IndexOf('\n', cut);
            if (nextNewline >= 0 && nextNewline < combined.Length - 1)
                combined = "[...log recortado...]" + Environment.NewLine + combined[(nextNewline + 1)..];
            else
                combined = combined[cut..];
        }
        OutputLog = combined;
    }

}
