using System.IO;
using System.Text;
using System.Windows;
using DexSuite.App.Data;
using DexSuite.App.Services;
using DexSuite.App.Services.CleanupModules;
using DexSuite.App.Services.Licensing;
using DexSuite.App.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace DexSuite.App;

/// <summary>
/// Entry point WPF. Construimos el Host de Microsoft.Extensions.Hosting (DI + lifetime + config)
/// y resolvemos la MainWindow desde el contenedor para que reciba sus dependencias
/// (ViewModels, Services) inyectadas.
/// </summary>
public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // CodePage 1252 sigue siendo útil para procesos de Windows que escupen
        // OEM/Western encoding (algunas líneas de netsh, defrag) en M16/M18.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var logsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DexSuite", "logs");
        Directory.CreateDirectory(logsDir);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(logsDir, "dexsuite-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                encoding: Encoding.UTF8)   // evita caracteres garbled en sistemas con codepage ANSI
            .CreateLogger();

        _host = Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices((_, services) => ConfigureServices(services))
            .Build();

        // Verificar integridad antes de mostrar UI.
        try
        {
            var integrity = _host.Services.GetRequiredService<IIntegrityVerifier>();
            if (!integrity.Verify(out var reason))
            {
                Log.Error("Integridad del ejecutable fallida: {Reason}", reason);
                MessageBox.Show(
                    $"DexSuite no puede arrancar:{Environment.NewLine}{Environment.NewLine}{reason}",
                    "DexSuite — Error de integridad",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(1);
                return;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Excepción al verificar integridad — la app continúa por seguridad de la UX");
        }

        // Asegura que la BD SQLite existe (crea esquema si es la 1ª ejecución).
        // Se hace ANTES de _host.Start() para que el LicenseWatchdog (IHostedService)
        // encuentre las tablas listas desde su primer tick.
        try
        {
            using var scope = _host.Services.CreateScope();
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<DexSuiteDbContext>>();
            using var db = factory.CreateDbContext();
            db.Database.EnsureCreated();
            EnsureModuleChangesTable(db);
            EnsureModuleStatesTable(db);
            EnsureLicensesTable(db);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "No se pudo inicializar la base de datos del historial");
        }

        // Arranca los hosted services (LicenseWatchdog, etc.) una vez que la BD está lista.
        _host.Start();

        // Carga inicial de la licencia (sincrónica, antes de la primera vista).
        // El servicio re-verifica la firma desde cero; si la licencia es válida,
        // CurrentTier queda con Avanzado/Pro y MainViewModel desbloquea módulos.
        try
        {
            var license = _host.Services.GetRequiredService<ILicenseService>();
            await license.RevalidateAsync();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "No se pudo cargar la licencia al arrancar");
        }

        // Aplica el tema persistido (o Default si es la primera ejecución).
        // Se hace antes de mostrar la ventana para evitar el "flash" del tema antiguo.
        var themeService = _host.Services.GetRequiredService<IThemeService>();
        var persistedTheme = themeService.LoadPersistedTheme();
        themeService.ApplyTheme(persistedTheme);

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            // Flush de ajustes pendientes antes de cerrar el host (evita perder
            // cambios escritos en los últimos 400 ms del debounce).
            try
            {
                var settingsService = _host.Services.GetService<ISettingsService>();
                settingsService?.FlushAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Fallo al hacer flush de settings al salir");
            }

            _host.StopAsync(TimeSpan.FromSeconds(3)).GetAwaiter().GetResult();
            _host.Dispose();
        }
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    /// <summary>
    /// Crea de forma idempotente la tabla ModuleChanges. Usuarios con BD
    /// previa no la tienen porque EnsureCreated solo crea esquema si la BD no existía.
    /// Mantenemos la migración manual aquí en lugar de añadir EF Migrations
    /// para evitar el overhead hasta que sea estrictamente necesario.
    /// </summary>
    private static void EnsureModuleChangesTable(DexSuiteDbContext db)
    {
        const string sql = @"
            CREATE TABLE IF NOT EXISTS ModuleChanges (
                Id              INTEGER NOT NULL CONSTRAINT PK_ModuleChanges PRIMARY KEY AUTOINCREMENT,
                ModuleId        TEXT NOT NULL,
                ModuleName      TEXT NOT NULL,
                ChangeType      INTEGER NOT NULL,
                Target          TEXT NOT NULL,
                SubTarget       TEXT NULL,
                OriginalValue   TEXT NULL,
                NewValue        TEXT NULL,
                ValueKind       TEXT NULL,
                AppliedAtUtc    TEXT NOT NULL,
                IsReverted      INTEGER NOT NULL DEFAULT 0,
                RevertedAtUtc   TEXT NULL,
                RevertError     TEXT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_ModuleChanges_ModuleId
                ON ModuleChanges (ModuleId);
            CREATE INDEX IF NOT EXISTS IX_ModuleChanges_IsReverted_AppliedAtUtc
                ON ModuleChanges (IsReverted, AppliedAtUtc DESC);
        ";
        db.Database.ExecuteSqlRaw(sql);
    }

    /// <summary>
    /// Crea de forma idempotente la tabla ModuleStates (estado "aplicado" por
    /// módulo, persistido entre sesiones). Usuarios con BD previa no la tienen.
    /// </summary>
    private static void EnsureModuleStatesTable(DexSuiteDbContext db)
    {
        const string sql = @"
            CREATE TABLE IF NOT EXISTS ModuleStates (
                ModuleId        INTEGER NOT NULL CONSTRAINT PK_ModuleStates PRIMARY KEY,
                IsApplied       INTEGER NOT NULL DEFAULT 0,
                AppliedAtUtc    TEXT NULL
            );
        ";
        db.Database.ExecuteSqlRaw(sql);
    }

    /// <summary>
    /// Crea de forma idempotente la tabla Licenses.
    /// Solo se mantiene una fila a la vez; LicenseService borra y reinserta al
    /// activar una licencia nueva.
    /// </summary>
    private static void EnsureLicensesTable(DexSuiteDbContext db)
    {
        const string sql = @"
            CREATE TABLE IF NOT EXISTS Licenses (
                Id              INTEGER NOT NULL CONSTRAINT PK_Licenses PRIMARY KEY AUTOINCREMENT,
                Hwid            TEXT NOT NULL,
                Tier            INTEGER NOT NULL,
                LicenseId       TEXT NOT NULL,
                Blob            TEXT NOT NULL,
                IssuedAtUtc     TEXT NOT NULL,
                AppliedAtUtc    TEXT NOT NULL
            );
        ";
        db.Database.ExecuteSqlRaw(sql);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Carpeta %LocalAppData%/DexSuite (compartida por logs Serilog y BD).
        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DexSuite");
        Directory.CreateDirectory(dataDir);
        var dbPath = Path.Combine(dataDir, "dexsuite.db");

        // EF Core: factory para que el servicio cree DbContext por operación,
        // evitando compartir contexto entre hilos.
        services.AddDbContextFactory<DexSuiteDbContext>(opt =>
            opt.UseSqlite($"Data Source={dbPath}"));

        // Services
        services.AddSingleton<IModuleCatalog, ModuleCatalog>();
        services.AddSingleton<INativeModuleRunner, NativeModuleRunner>();
        services.AddSingleton<IPerformanceAnalyzer, PerformanceAnalyzer>();
        services.AddSingleton<IUpdateService, VelopackUpdateService>();
        services.AddSingleton<IQuickCleanService, QuickCleanService>();
        services.AddSingleton<ISystemInfoService, SystemInfoService>();
        services.AddSingleton<IAppLogService, AppLogService>();
        services.AddSingleton<IPerformanceBaselineService, PerformanceBaselineService>();
        services.AddSingleton<IRestorePointService, RestorePointService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IGameOptimizationService, GameOptimizationService>();
        services.AddSingleton<IWingetService, WingetService>();
        services.AddSingleton<ISecurityCheckService, SecurityCheckService>();
        services.AddSingleton<IChangeTrackingService, ChangeTrackingService>();
        services.AddSingleton<IModuleStateService, ModuleStateService>();
        services.AddSingleton<IBugReportService, BugReportService>();
        services.AddSingleton<IAppSelfCleanupService, AppSelfCleanupService>();

        // Sistema de licencias
        services.AddSingleton<IHardwareIdProvider, HardwareIdProvider>();
        services.AddSingleton<IIntegrityVerifier, IntegrityVerifier>();
        services.AddSingleton<ILicenseService, LicenseService>();
        // Watchdog re-verifica la licencia cada ~10 min con jitter; hospedado
        // como BackgroundService para que el host lo arranque y pare con la app.
        services.AddHostedService<LicenseWatchdog>();

        // Executors nativos de los 19 módulos, registrados como IModuleExecutor
        // para que NativeModuleRunner los descubra en orden ascendente de ModuleId.
        services.AddSingleton<IModuleExecutor, M01Prefetch>();
        services.AddSingleton<IModuleExecutor, M02SystemLogs>();
        services.AddSingleton<IModuleExecutor, M03TempAndRecycle>();
        services.AddSingleton<IModuleExecutor, M04DeepCleanup>();
        services.AddSingleton<IModuleExecutor, M05WindowsUpdate>();
        services.AddSingleton<IModuleExecutor, M06DismComponentStore>();
        services.AddSingleton<IModuleExecutor, M07BrowserCache>();
        services.AddSingleton<IModuleExecutor, M08NetworkReset>();
        services.AddSingleton<IModuleExecutor, M09StoreOneDriveTeams>();
        services.AddSingleton<IModuleExecutor, M10SfcDism>();
        services.AddSingleton<IModuleExecutor, M11Peripherals>();
        services.AddSingleton<IModuleExecutor, M12WingetUpgrade>();
        services.AddSingleton<IModuleExecutor, M13CopilotCortanaTelemetry>();
        services.AddSingleton<IModuleExecutor, M14PrivacyServices>();
        services.AddSingleton<IModuleExecutor, M15Performance>();
        services.AddSingleton<IModuleExecutor, M16Ethernet>();
        services.AddSingleton<IModuleExecutor, M17Security>();
        services.AddSingleton<IModuleExecutor, M18SsdTrim>();
        services.AddSingleton<IModuleExecutor, M19Drivers>();

        // i18n: el singleton estático es el mismo que usa la markup extension {loc:T}.
        services.AddSingleton<ILocalizationService>(LocalizationService.Instance);
        services.AddSingleton<IHelpService, HelpService>();

        // ViewModels
        services.AddSingleton<MainViewModel>();
        // Game selector: Transient porque cada apertura empieza con tiles
        // limpios (selección de variante no se preserva entre sesiones).
        services.AddTransient<GameSelectorViewModel>();

        // Views
        services.AddSingleton<MainWindow>();
        services.AddTransient<GameSelectorWindow>();
    }
}
