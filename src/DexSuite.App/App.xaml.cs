using System.IO;
using System.Text;
using System.Windows;
using DexSuite.App.Services;
using DexSuite.App.ViewModels;
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

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Sin esto, Encoding.GetEncoding(1252) lanza en .NET Core/+5. Lo necesita BatRunner
        // para leer la salida del .bat que corre con "chcp 1252".
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
                retainedFileCountLimit: 7)
            .CreateLogger();

        _host = Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices((_, services) => ConfigureServices(services))
            .Build();

        _host.Start();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            _host.StopAsync(TimeSpan.FromSeconds(3)).GetAwaiter().GetResult();
            _host.Dispose();
        }
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Services
        services.AddSingleton<IModuleCatalog, ModuleCatalog>();
        services.AddSingleton<IBatRunner, BatRunner>();
        services.AddSingleton<IPerformanceAnalyzer, PerformanceAnalyzer>();
        services.AddSingleton<IUpdateService, VelopackUpdateService>();

        // i18n: el singleton estático es el mismo que usa la markup extension {loc:T}.
        services.AddSingleton<ILocalizationService>(LocalizationService.Instance);
        services.AddSingleton<IHelpService, HelpService>();

        // ViewModels
        services.AddSingleton<MainViewModel>();

        // Views
        services.AddSingleton<MainWindow>();
    }
}
