using Velopack;

namespace DexSuite.App;

/// <summary>
/// Punto de entrada manual.
/// VelopackApp.Build().Run() DEBE ejecutarse antes de que WPF inicialice
/// cualquier ventana, para que los hooks de instalación/desinstalación
/// funcionen correctamente.
/// </summary>
public static class Program
{
    [System.STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build().Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
