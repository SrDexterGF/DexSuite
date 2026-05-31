using DexSuite.App.ViewModels;
using System.ComponentModel;
using System.Windows;
using Wpf.Ui.Controls;
using WpfMenuItem    = System.Windows.Controls.MenuItem;
using WpfSeparator   = System.Windows.Controls.Separator;
using WpfContextMenu = System.Windows.Controls.ContextMenu;

namespace DexSuite.App;

public partial class MainWindow : FluentWindow
{
    private MainViewModel? _vm;

    // True solo cuando el usuario pide salir de verdad (menú "Cerrar DexSuite"),
    // para que OnWindowClosing no cancele ese cierre intencionado.
    private bool _forceClose;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        _vm = viewModel;

        StateChanged += OnWindowStateChanged;
        Closing       += OnWindowClosing;
        Closed        += OnWindowClosed;
        Loaded        += OnLoaded;
    }

    // ── Bandeja del sistema ────────────────────────────────────────────────────

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // El TaskbarIcon ya está en el árbol visual (declarado en XAML con x:Name).
        // Aquí solo añadimos su menú contextual y suscribimos los eventos.
        var restoreItem = new WpfMenuItem { Header = "Restaurar" };
        restoreItem.Click += (_, _) => RestoreWindow();

        var exitItem = new WpfMenuItem { Header = "Cerrar DexSuite" };
        exitItem.Click += (_, _) => { _forceClose = true; Application.Current.Shutdown(); };

        var menu = new WpfContextMenu();
        menu.Items.Add(restoreItem);
        menu.Items.Add(new WpfSeparator());
        menu.Items.Add(exitItem);

        TrayIcon.ContextMenu  = menu;
        TrayIcon.TrayMouseDoubleClick += (_, _) => Dispatcher.Invoke(RestoreWindow);

        RefreshTrayVisibility();
        if (_vm is not null)
            _vm.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.MinimizeToTray))
            Dispatcher.Invoke(RefreshTrayVisibility);
    }

    // Mientras "minimizar a bandeja" esté activado:
    //  - el icono de bandeja está SIEMPRE visible (así el usuario puede recuperar
    //    la ventana aunque la haya ocultado),
    //  - y la ventana desaparece de la barra de tareas (ShowInTaskbar = false)
    //    para no duplicarse con el icono de la bandeja.
    private void RefreshTrayVisibility()
    {
        if (_vm is null) return;
        if (_vm.MinimizeToTray)
        {
            TrayIcon.Visibility = Visibility.Visible;
            ShowInTaskbar       = false;
        }
        else
        {
            TrayIcon.Visibility = Visibility.Collapsed;
            ShowInTaskbar       = true;
        }
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        if (_vm is null || !_vm.MinimizeToTray) return;

        if (WindowState == WindowState.Minimized)
        {
            // Oculta la ventana del Alt+Tab y del escritorio; el icono de bandeja
            // ya está visible (lo deja RefreshTrayVisibility cuando la opción
            // está activa), así que el usuario podrá recuperarla.
            Hide();
        }
    }

    // Con "minimizar a bandeja" activado, pulsar la X oculta la ventana al área
    // de notificaciones en lugar de cerrar la app. El cierre real solo ocurre
    // desde el menú "Cerrar DexSuite" del icono de bandeja (_forceClose = true).
    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_vm is null || !_vm.MinimizeToTray || _forceClose) return;

        e.Cancel = true;
        Hide();
    }

    private void RestoreWindow()
    {
        if (!IsVisible) Show();
        if (WindowState != WindowState.Normal)
            WindowState = WindowState.Normal;
        // Si la opción de bandeja está OFF, recuperamos la entrada en la taskbar.
        if (_vm is not null && !_vm.MinimizeToTray)
            ShowInTaskbar = true;
        Activate();
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (_vm is not null)
            _vm.PropertyChanged -= OnVmPropertyChanged;
        TrayIcon?.Dispose();
    }

    // ── Eventos de interfaz ────────────────────────────────────────────────────

    private void OnQuestionMarkPreventClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        e.Handled = true;
    }
}
