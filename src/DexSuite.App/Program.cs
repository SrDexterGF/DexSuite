using System.Diagnostics;
using System.Security.Principal;
using System.Threading;
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
    // Nombres globales para que funcionen entre el proceso elevado y cualquier
    // relanzamiento. Mutex = detecta instancia ya viva; Event = le pide que
    // muestre su ventana (cuando está oculta en la bandeja).
    private const string MutexName = @"Global\DexSuite_SingleInstance";
    private const string ShowEventName = @"Global\DexSuite_ShowWindow";

    // static para que no los recoja el GC durante la vida de la app.
    private static Mutex? _instanceMutex;
    private static EventWaitHandle? _showEvent;

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

        // Paso 3 — Instancia única. Si ya hay una viva (p. ej. oculta en la
        // bandeja), le pedimos que muestre su ventana y salimos, en vez de
        // arrancar un proceso duplicado.
        _instanceMutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            try
            {
                using var ev = EventWaitHandle.OpenExisting(ShowEventName);
                ev.Set();
            }
            catch { /* la otra instancia puede estar cerrándose; nada que hacer */ }
            return;
        }

        // Paso 4 — Inicializar WPF con privilegios de administrador.
        var app = new App();
        app.InitializeComponent();

        // Listener: cuando otra instancia intente abrir, reactiva la ventana.
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        var listener = new Thread(() =>
        {
            while (_showEvent.WaitOne())
                app.Dispatcher.BeginInvoke(new Action(app.BringToForeground));
        })
        { IsBackground = true };
        listener.Start();

        app.Run();
    }

    private static bool IsRunningAsAdmin()
    {
        using var identity  = WindowsIdentity.GetCurrent();
        var       principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}
