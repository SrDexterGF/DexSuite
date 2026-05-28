using System.Diagnostics;
using System.Security.Principal;
using Velopack;

namespace DexSuite.App;

/// <summary>
/// Punto de entrada manual.
///
/// Orden obligatorio:
///   1. VelopackApp.Build().Run() — debe ser lo primero para que los hooks
///      de instalación/actualización de Velopack funcionen antes de que WPF
///      inicialice ninguna ventana.
///   2. Comprobación de privilegios — si el proceso no es administrador se
///      relanza con el verbo "runas" (UAC) y el proceso actual termina.
///      El manifest usa asInvoker para que el launcher de Velopack pueda
///      arrancar la app sin elevar; la elevación real la gestionamos aquí.
///   3. Inicialización WPF — solo llega el proceso ya elevado.
/// </summary>
public static class Program
{
    [System.STAThread]
    public static void Main(string[] args)
    {
        // Paso 1 — Velopack siempre primero.
        VelopackApp.Build().Run();

        // Paso 2 — Elevar si es necesario.
        if (!IsRunningAsAdmin())
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName        = Environment.ProcessPath!,
                    Verb            = "runas",
                    UseShellExecute = true,
                    Arguments       = string.Join(" ", args),
                });
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // El usuario canceló el UAC — salir silenciosamente.
            }
            return;
        }

        // Paso 3 — Inicializar WPF con privilegios de administrador.
        var app = new App();
        app.InitializeComponent();
        app.Run();
    }

    private static bool IsRunningAsAdmin()
    {
        using var identity  = WindowsIdentity.GetCurrent();
        var       principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}
