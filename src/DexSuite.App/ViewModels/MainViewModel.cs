using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DexSuite.App.Models;
using DexSuite.App.Services;
using Microsoft.Extensions.Logging;

namespace DexSuite.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IBatRunner _runner;
    private readonly IPerformanceAnalyzer _analyzer;
    private readonly IUpdateService _updateService;
    private readonly ILocalizationService _loc;
    private readonly ILogger<MainViewModel> _logger;
    private readonly IQuickCleanService _quickClean;
    private readonly ISystemInfoService _systemInfo;
    private readonly IAppLogService _appLog;
    private readonly IPerformanceBaselineService _baseline;

    private const string DefaultScriptFolder =
        @"C:\Users\mgf74\Documents\Claude Environment W11\DexSuite (Script)";

    // CTS del run en curso, para que el botón Cancelar pueda matarlo.
    private CancellationTokenSource? _runCts;

    // Acumulador de líneas pendientes que aún no se han volcado a OutputLog.
    private readonly StringBuilder _pendingBuffer = new();
    private readonly object _bufferLock = new();
    private const int MaxLogChars = 80_000;

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

    /// <summary>True cuando la app no está ejecutando el .bat, analizando ni limpiando.</summary>
    public bool IsIdle => !IsRunning && !IsAnalyzing && !IsQuickCleaning;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    // ---- Helper i18n -----------------------------------------------------
    // T() = "translate", abreviado para no inflar las asignaciones de StatusMessage.

    /// <summary>Traduce una clave i18n al idioma activo.</summary>
    private string T(string key) => _loc.Get(key);

    /// <summary>Traduce una clave i18n y aplica string.Format con los argumentos.</summary>
    private string T(string key, params object?[] args)
        => string.Format(_loc.Get(key), args);

    // ---- Navegación entre secciones (sidebar) ----------------------------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHomeView))]
    [NotifyPropertyChangedFor(nameof(IsModulesView))]
    [NotifyPropertyChangedFor(nameof(IsLogView))]
    [NotifyPropertyChangedFor(nameof(IsSpecsView))]
    [NotifyPropertyChangedFor(nameof(IsSettingsView))]
    [NotifyPropertyChangedFor(nameof(IsUpdatesView))]
    [NotifyPropertyChangedFor(nameof(IsAboutView))]
    private AppSection currentSection = AppSection.Home;

    public bool IsHomeView     => CurrentSection == AppSection.Home;
    public bool IsModulesView  => CurrentSection == AppSection.Modules;
    public bool IsLogView      => CurrentSection == AppSection.Log;
    public bool IsSpecsView    => CurrentSection == AppSection.Specs;
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
        }
    }

    // ---- Especificaciones del sistema -----------------------------------

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

    // ---- Historial interno (SQLite) -------------------------------------

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
            OnPropertyChanged(nameof(HasAppLogEntries));
            OnPropertyChanged(nameof(IsAppLogEmpty));
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
        OnPropertyChanged(nameof(HasAppLogEntries));
        OnPropertyChanged(nameof(IsAppLogEmpty));
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

            OnPropertyChanged(nameof(HasAppLogEntries));
            OnPropertyChanged(nameof(IsAppLogEmpty));
        });
    }

    // ---- Búsqueda de módulos --------------------------------------------

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

    // ---- Acciones rápidas sobre la lista de módulos ----------------------

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var m in Modules) m.IsEnabled = true;
    }

    [RelayCommand]
    private void DeselectAll()
    {
        foreach (var m in Modules) m.IsEnabled = false;
    }

    [RelayCommand]
    private void SelectRecommended()
    {
        foreach (var m in Modules) m.IsEnabled = m.Module.RecommendedDefault;
    }

    // ---- Idioma ----------------------------------------------------------

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

    // ---- Ajustes ---------------------------------------------------------

    /// <summary>Si la lista de módulos arranca con los recomendados ya marcados.</summary>
    [ObservableProperty]
    private bool autoSelectRecommended = true;

    /// <summary>Si al pulsar Ejecutar saltamos automáticamente a la vista de Registro.</summary>
    [ObservableProperty]
    private bool jumpToLogOnRun = true;

    /// <summary>Avisar antes de ejecutar módulos no reversibles (futuro).</summary>
    [ObservableProperty]
    private bool warnBeforeNonReversible = true;

    /// <summary>Mostrar notificación de Windows al terminar (futuro).</summary>
    [ObservableProperty]
    private bool notifyOnFinish;

    /// <summary>Tier activo del usuario. Controla qué módulos puede ejecutar.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UserTierEnum))]
    private string userTier = "Pro";

    /// <summary>Tier activo como enum comparable con <see cref="ModuleTier"/>.</summary>
    public ModuleTier UserTierEnum => UserTier switch
    {
        "Avanzado" => ModuleTier.Advanced,
        "Pro"      => ModuleTier.Pro,
        _          => ModuleTier.Free,
    };

    /// <summary>Cada vez que cambia el tier, recalcula qué módulos quedan bloqueados.</summary>
    partial void OnUserTierChanged(string value)
    {
        UpdateModuleLockStates();
        _ = _appLog.InfoAsync(AppLogCategory.Settings, T("Log.Event.TierChanged", value));
    }

    private void UpdateModuleLockStates()
    {
        var currentTier = UserTierEnum;
        foreach (var m in Modules)
            m.IsLocked = m.Module.Tier > currentTier;
    }

    /// <summary>Opciones disponibles para el ComboBox de tier.</summary>
    public IReadOnlyList<string> AvailableTiers { get; } = new[] { "Free", "Avanzado", "Pro" };

    /// <summary>Opciones disponibles para el ComboBox de canal de actualización.</summary>
    public IReadOnlyList<string> AvailableChannels { get; } = new[] { "Stable", "Beta" };

    /// <summary>Carpeta donde Serilog escribe los logs (calculada al construir el VM).</summary>
    public string LogsFolder { get; }

    /// <summary>Carpeta donde busca el .bat de DexSuite. Editable en Ajustes.</summary>
    [ObservableProperty]
    private string scriptFolder = DefaultScriptFolder;

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
            StatusMessage = $"No se pudo abrir la carpeta: {ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenScriptFolder()
    {
        try
        {
            if (!Directory.Exists(ScriptFolder))
            {
                StatusMessage = $"La carpeta no existe: {ScriptFolder}";
                return;
            }
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = ScriptFolder,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"No se pudo abrir la carpeta: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ResetSettings()
    {
        AutoSelectRecommended = true;
        JumpToLogOnRun = true;
        WarnBeforeNonReversible = true;
        NotifyOnFinish = false;
        UserTier = "Pro";
        ScriptFolder = DefaultScriptFolder;
        StatusMessage = T("Status.SettingsReset");
        _ = _appLog.InfoAsync(AppLogCategory.Settings, T("Log.Event.SettingsReset"));
    }

    // ---- Actualizaciones -------------------------------------------------

    /// <summary>Versión instalada actual, reportada por Velopack (o "0.1.0" en dev).</summary>
    public string CurrentVersion => _updateService.CurrentVersion;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LastUpdateCheckLabel))]
    private string lastUpdateCheck = "Nunca";

    /// <summary>Etiqueta localizada "Última comprobación: ...".</summary>
    public string LastUpdateCheckLabel => T("Updates.LastCheck", LastUpdateCheck);

    [ObservableProperty]
    private bool autoUpdateEnabled = true;

    [ObservableProperty]
    private string updateChannel = "Stable";

    /// <summary>Versión disponible para descargar, null si no hay actualización.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAvailableUpdate))]
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

    // ---- Test de rendimiento --------------------------------------------

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

    // ---- Limpieza rápida ------------------------------------------------

    [RelayCommand(CanExecute = nameof(CanQuickClean))]
    private async Task QuickCleanAsync()
    {
        IsQuickCleaning = true;
        QuickCleanCommand.NotifyCanExecuteChanged();
        RunCommand.NotifyCanExecuteChanged();
        AnalyzeCommand.NotifyCanExecuteChanged();
        StatusMessage = T("Status.QuickCleaning");

        try
        {
            var result = await _quickClean.CleanAsync();
            var mb = Math.Round(result.BytesFreed / 1_048_576.0, 1);
            StatusMessage = T("Status.QuickCleanDone", mb, result.FilesDeleted);
            await _appLog.SuccessAsync(AppLogCategory.QuickClean,
                T("Log.Event.QuickCleanFinished", mb, result.FilesDeleted));
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

    // ---- Constructor -----------------------------------------------------

    public MainViewModel(
        IModuleCatalog catalog,
        IBatRunner runner,
        IPerformanceAnalyzer analyzer,
        IUpdateService updateService,
        ILocalizationService loc,
        IQuickCleanService quickClean,
        ISystemInfoService systemInfo,
        IAppLogService appLog,
        IPerformanceBaselineService baseline,
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
        _logger = logger;

        // Mensaje inicial localizado. Cuando el usuario cambia el idioma,
        // re-emitimos el mensaje de "Listo" si seguimos en ese estado.
        StatusMessage = T("Status.Ready");
        _loc.LanguageChanged += (_, _) =>
        {
            // Re-emitimos propiedades cuyo texto depende del idioma:
            OnPropertyChanged(nameof(CurrentLanguage));
            OnPropertyChanged(nameof(LastUpdateCheckLabel));
            OnPropertyChanged(nameof(AvailableUpdateVersionLabel));
            // Registramos el cambio en el historial interno.
            _ = _appLog.InfoAsync(AppLogCategory.Language,
                T("Log.Event.LanguageChanged", CurrentLanguage));
            // El StatusMessage es un mensaje puntual; no lo refrescamos.
            // Las próximas asignaciones ya saldrán en el idioma nuevo.
        };

        // Suscripción al historial para refresco en vivo de la vista.
        _appLog.EntryAdded += OnAppLogEntryAdded;

        LogsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DexSuite", "logs");

        foreach (var m in catalog.GetAll())
        {
            var vm = new ModuleItemViewModel(m, initiallyEnabled: AutoSelectRecommended && m.RecommendedDefault, _loc);
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

        // Carga el baseline guardado en disco (fire-and-forget — no bloquea la UI).
        _ = LoadPersistedBaselineAsync();

        // Evento de arranque en el historial interno (fire-and-forget).
        _ = _appLog.InfoAsync(AppLogCategory.App,
            T("Log.Event.AppStarted", _updateService.CurrentVersion));
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

    // ---- Ejecutar / Cancelar --------------------------------------------

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task RunAsync()
    {
        var selected = Modules.Where(m => m.IsEnabled).Select(m => m.Id).OrderBy(id => id).ToList();
        if (selected.Count == 0)
        {
            StatusMessage = T("Status.SelectModulesFirst");
            return;
        }

        string batPath;
        try
        {
            batPath = ResolveBatPath();
        }
        catch (FileNotFoundException ex)
        {
            StatusMessage = $"[!] {ex.Message}";
            _logger.LogError(ex, "No se encontró ningún .bat de DexSuite");
            return;
        }

        IsRunning = true;
        OutputLog = string.Empty;
        lock (_bufferLock) _pendingBuffer.Clear();
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
            _logger.LogInformation("Lanzando .bat {Path} con módulos: {Modules}", batPath, string.Join(",", selected));
            var modulesStr = string.Join(", ", Modules.Where(m => m.IsEnabled).Select(m => m.Name));
            await _appLog.InfoAsync(AppLogCategory.Run,
                T("Log.Event.RunStarted", selected.Count),
                modulesStr);

            await foreach (var line in _runner.RunAsync(batPath, selected, ct).WithCancellation(ct))
            {
                lock (_bufferLock) _pendingBuffer.AppendLine(line);
            }
            StatusMessage = T("Status.ExecutionDone");
            await _appLog.SuccessAsync(AppLogCategory.Run, T("Log.Event.RunFinished", selected.Count));
        }
        catch (OperationCanceledException)
        {
            StatusMessage = T("Status.ExecutionCancelled");
            await _appLog.WarningAsync(AppLogCategory.Run, T("Log.Event.RunCancelled"));
        }
        catch (Exception ex)
        {
            StatusMessage = T("Status.ExecutionError", ex.Message);
            _logger.LogError(ex, "Fallo al ejecutar el .bat");
            await _appLog.ErrorAsync(AppLogCategory.Run, T("Log.Event.RunFailed", ex.Message), ex.ToString());
        }
        finally
        {
            flushCts.Cancel();
            try { await flushTask; } catch { /* swallowed */ }
            FlushBuffer();

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

    // ---- Helpers internos ------------------------------------------------

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

    /// <summary>
    /// Busca el .bat de DexSuite en la carpeta configurada (ScriptFolder) y devuelve
    /// el de versión más alta. Así sobrevivimos a actualizaciones como v0.9.0 -> v1.0.0.
    /// </summary>
    private string ResolveBatPath()
    {
        var scriptDir = ScriptFolder;
        if (!Directory.Exists(scriptDir))
            throw new FileNotFoundException(
                $"No existe la carpeta del .bat: {scriptDir}", scriptDir);

        var candidates = Directory.GetFiles(scriptDir, "DexSuite_CleanUp_v*.bat");
        if (candidates.Length == 0)
            throw new FileNotFoundException(
                $"No se encontró ningún DexSuite_CleanUp_v*.bat en {scriptDir}", scriptDir);

        var versionRegex = new Regex(@"v(\d+)\.(\d+)\.(\d+)", RegexOptions.IgnoreCase);
        var best = candidates
            .Select(path => new
            {
                Path = path,
                Version = ParseVersion(versionRegex, Path.GetFileName(path)),
            })
            .OrderByDescending(x => x.Version)
            .First();

        return best.Path;
    }

    private static Version ParseVersion(Regex regex, string fileName)
    {
        var m = regex.Match(fileName);
        if (!m.Success) return new Version(0, 0, 0);
        return new Version(
            int.Parse(m.Groups[1].Value),
            int.Parse(m.Groups[2].Value),
            int.Parse(m.Groups[3].Value));
    }
}
