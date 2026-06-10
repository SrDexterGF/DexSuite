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

        var settingsItem = new WpfMenuItem { Header = "Ajustes" };
        settingsItem.Click += (_, _) => { RestoreWindow(); _vm?.NavigateCommand.Execute("Settings"); };

        var aboutItem = new WpfMenuItem { Header = "Acerca de" };
        aboutItem.Click += (_, _) => { RestoreWindow(); _vm?.NavigateCommand.Execute("About"); };

        var updatesItem = new WpfMenuItem { Header = "Buscar actualizaciones" };
        updatesItem.Click += (_, _) => { RestoreWindow(); _vm?.NavigateCommand.Execute("Updates"); };

        var exitItem = new WpfMenuItem { Header = "Cerrar DexSuite" };
        exitItem.Click += (_, _) => { _forceClose = true; Application.Current.Shutdown(); };

        var menu = new WpfContextMenu();
        menu.Items.Add(restoreItem);
        menu.Items.Add(new WpfSeparator());
        menu.Items.Add(settingsItem);
        menu.Items.Add(aboutItem);
        menu.Items.Add(updatesItem);
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

    // El icono de bandeja solo aparece cuando la ventana está oculta.
    // Cuando MinimizeToTray está OFF el icono permanece siempre oculto.
    private void RefreshTrayVisibility()
    {
        if (_vm is null) return;
        if (_vm.MinimizeToTray && !IsVisible)
        {
            TrayIcon.Visibility = Visibility.Visible;
            ShowInTaskbar       = false;
        }
        else
        {
            TrayIcon.Visibility = Visibility.Collapsed;
            ShowInTaskbar       = _vm.MinimizeToTray ? false : true;
        }
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        if (_vm is null || !_vm.MinimizeToTray) return;

        if (WindowState == WindowState.Minimized)
        {
            Hide();
            RefreshTrayVisibility();
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
        RefreshTrayVisibility();
    }

    private void RestoreWindow()
    {
        if (!IsVisible) Show();
        if (WindowState != WindowState.Normal)
            WindowState = WindowState.Normal;
        ShowInTaskbar = !(_vm?.MinimizeToTray ?? false);
        TrayIcon.Visibility = Visibility.Collapsed;
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
