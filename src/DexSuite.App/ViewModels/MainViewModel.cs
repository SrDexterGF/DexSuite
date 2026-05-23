using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
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
    private readonly ILogger<MainViewModel> _logger;

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
    public ObservableCollection<ModuleItemViewModel> ProModules { get; } = new();

    [ObservableProperty]
    private string outputLog = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    private bool isRunning;

    /// <summary>True cuando la app no está ejecutando el .bat ni analizando rendimiento.</summary>
    public bool IsIdle => !IsRunning && !IsAnalyzing;

    [ObservableProperty]
    private string statusMessage = "Listo. Selecciona módulos y pulsa Ejecutar.";

    // ---- Navegación entre secciones (sidebar) ----------------------------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHomeView))]
    [NotifyPropertyChangedFor(nameof(IsModulesView))]
    [NotifyPropertyChangedFor(nameof(IsLogView))]
    [NotifyPropertyChangedFor(nameof(IsSettingsView))]
    [NotifyPropertyChangedFor(nameof(IsUpdatesView))]
    [NotifyPropertyChangedFor(nameof(IsAboutView))]
    private AppSection currentSection = AppSection.Home;

    public bool IsHomeView => CurrentSection == AppSection.Home;
    public bool IsModulesView => CurrentSection == AppSection.Modules;
    public bool IsLogView => CurrentSection == AppSection.Log;
    public bool IsSettingsView => CurrentSection == AppSection.Settings;
    public bool IsUpdatesView => CurrentSection == AppSection.Updates;
    public bool IsAboutView => CurrentSection == AppSection.About;

    [RelayCommand]
    private void Navigate(string sectionName)
    {
        if (Enum.TryParse<AppSection>(sectionName, ignoreCase: true, out var section))
            CurrentSection = section;
    }

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
    private string userTier = "Pro";

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
        StatusMessage = "Ajustes restablecidos a sus valores por defecto.";
    }

    // ---- Actualizaciones -------------------------------------------------

    /// <summary>Versión instalada actual, reportada por Velopack (o "0.1.0" en dev).</summary>
    public string CurrentVersion => _updateService.CurrentVersion;

    [ObservableProperty]
    private string lastUpdateCheck = "Nunca";

    [ObservableProperty]
    private bool autoUpdateEnabled = true;

    [ObservableProperty]
    private string updateChannel = "Stable";

    /// <summary>Versión disponible para descargar, null si no hay actualización.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAvailableUpdate))]
    private string? availableUpdateVersion;

    /// <summary>True cuando hay una actualización descargable lista.</summary>
    public bool HasAvailableUpdate => AvailableUpdateVersion is not null;

    [ObservableProperty]
    private int updateDownloadProgress;

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        StatusMessage = "Buscando actualizaciones...";
        try
        {
            var newVersion = await _updateService.CheckForUpdatesAsync();
            LastUpdateCheck = DateTime.Now.ToString("dd/MM/yyyy HH:mm");

            if (newVersion is not null)
            {
                AvailableUpdateVersion = newVersion;
                StatusMessage = $"¡Nueva versión disponible: v{newVersion}!\nPulsa «Actualizar ahora» para instalarla.";
            }
            else if (!_updateService.IsInstalledBuild)
            {
                StatusMessage = "Modo desarrollo — actualizaciones no disponibles en este entorno.";
                LastUpdateCheck = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
            }
            else
            {
                AvailableUpdateVersion = null;
                StatusMessage = $"Estás en la última versión disponible (v{CurrentVersion}).";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al buscar actualizaciones:\n{ex.Message}";
            _logger.LogError(ex, "Fallo al buscar actualizaciones");
        }
        ApplyUpdateCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(HasAvailableUpdate))]
    private async Task ApplyUpdateAsync()
    {
        StatusMessage = "Descargando actualización...";
        UpdateDownloadProgress = 0;
        try
        {
            var progress = new Progress<int>(p =>
            {
                UpdateDownloadProgress = p;
                StatusMessage = $"Descargando actualización... {p}%";
            });
            await _updateService.DownloadAndApplyAsync(progress);
            // Si llegamos aquí es que no se reinició (entorno no instalado).
            StatusMessage = "Actualización descargada.\nLa aplicación se reiniciará automáticamente.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al aplicar la actualización:\n{ex.Message}";
            _logger.LogError(ex, "Fallo al aplicar actualización");
        }
    }

    // ---- Test de rendimiento --------------------------------------------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasScoreBefore))]
    [NotifyPropertyChangedFor(nameof(HasScoreAfter))]
    [NotifyPropertyChangedFor(nameof(HasComparison))]
    [NotifyPropertyChangedFor(nameof(ScoreDelta))]
    [NotifyPropertyChangedFor(nameof(ScoreDeltaLabel))]
    private PerformanceScore? scoreBefore;

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

    [RelayCommand(CanExecute = nameof(CanAnalyze))]
    private async Task AnalyzeAsync()
    {
        IsAnalyzing = true;
        AnalyzeCommand.NotifyCanExecuteChanged();
        ResetScoresCommand.NotifyCanExecuteChanged();
        RunCommand.NotifyCanExecuteChanged();

        StatusMessage = "Analizando rendimiento...";

        try
        {
            var score = await _analyzer.AnalyzeAsync();
            if (ScoreBefore is null)
            {
                ScoreBefore = score;
                StatusMessage = $"Análisis inicial: {score.Total}/100 ({score.Verdict}).\nYa puedes optimizar.";
            }
            else
            {
                ScoreAfter = score;
                var delta = score.Total - ScoreBefore.Total;
                var sign = delta >= 0 ? "+" : "";
                StatusMessage = $"Análisis final: {score.Total}/100 ({score.Verdict}).\nDiferencia: {sign}{delta} puntos.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al analizar: {ex.Message}";
            _logger.LogError(ex, "Fallo el analisis de rendimiento");
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
        StatusMessage = "Puntuaciones reiniciadas. Pulsa Analizar para empezar de nuevo.";
        AnalyzeCommand.NotifyCanExecuteChanged();
        ResetScoresCommand.NotifyCanExecuteChanged();
    }

    private bool CanAnalyze() => !IsAnalyzing && !IsRunning;
    private bool CanResetScores() => !IsAnalyzing && (ScoreBefore is not null || ScoreAfter is not null);

    partial void OnScoreBeforeChanged(PerformanceScore? value)
        => ResetScoresCommand.NotifyCanExecuteChanged();

    partial void OnScoreAfterChanged(PerformanceScore? value)
        => ResetScoresCommand.NotifyCanExecuteChanged();

    // ---- Constructor -----------------------------------------------------

    public MainViewModel(
        IModuleCatalog catalog,
        IBatRunner runner,
        IPerformanceAnalyzer analyzer,
        IUpdateService updateService,
        ILogger<MainViewModel> logger)
    {
        _runner = runner;
        _analyzer = analyzer;
        _updateService = updateService;
        _logger = logger;

        LogsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DexSuite", "logs");

        foreach (var m in catalog.GetAll())
        {
            var vm = new ModuleItemViewModel(m, initiallyEnabled: AutoSelectRecommended && m.RecommendedDefault);
            Modules.Add(vm);
            switch (m.Tier)
            {
                case ModuleTier.Free: FreeModules.Add(vm); break;
                case ModuleTier.Advanced: AdvancedModules.Add(vm); break;
                case ModuleTier.Pro: ProModules.Add(vm); break;
            }
        }
    }

    // ---- Ejecutar / Cancelar --------------------------------------------

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task RunAsync()
    {
        var selected = Modules.Where(m => m.IsEnabled).Select(m => m.Id).OrderBy(id => id).ToList();
        if (selected.Count == 0)
        {
            StatusMessage = "Selecciona al menos un módulo antes de ejecutar.";
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
        StatusMessage = $"Ejecutando {selected.Count} módulo(s)... (puedes cancelar)";
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
            await foreach (var line in _runner.RunAsync(batPath, selected, ct).WithCancellation(ct))
            {
                lock (_bufferLock) _pendingBuffer.AppendLine(line);
            }
            StatusMessage = "Ejecución completada.\nVuelve a Inicio y analiza para comparar.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Ejecución cancelada por el usuario.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            _logger.LogError(ex, "Fallo al ejecutar el .bat");
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
        StatusMessage = "Cancelando...";
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
