using DexSuite.App.ViewModels;
using H.NotifyIcon;
using System.ComponentModel;
using System.Windows;
using Wpf.Ui.Controls;
using WpfMenuItem    = System.Windows.Controls.MenuItem;
using WpfSeparator   = System.Windows.Controls.Separator;
using WpfContextMenu = System.Windows.Controls.ContextMenu;

namespace DexSuite.App;

public partial class MainWindow : FluentWindow
{
    private TaskbarIcon? _trayIcon;
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
        _trayIcon = (TaskbarIcon?)TryFindResource("TrayIcon");
        if (_trayIcon is null) return;

        var restoreItem = new WpfMenuItem { Header = "Restaurar" };
        restoreItem.Click += (_, _) => RestoreWindow();

        var exitItem = new WpfMenuItem { Header = "Cerrar DexSuite" };
        exitItem.Click += (_, _) => { _forceClose = true; Application.Current.Shutdown(); };

        var menu = new WpfContextMenu();
        menu.Items.Add(restoreItem);
        menu.Items.Add(new WpfSeparator());
        menu.Items.Add(exitItem);

        _trayIcon.ContextMenu  = menu;
        _trayIcon.TrayMouseDoubleClick += (_, _) => Dispatcher.Invoke(RestoreWindow);

        // Show the icon immediately if MinimizeToTray is already enabled,
        // and track future changes to the setting.
        RefreshTrayVisibility();
        if (_vm is not null)
            _vm.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.MinimizeToTray))
            Dispatcher.Invoke(RefreshTrayVisibility);
    }

    // The icon stays visible for as long as MinimizeToTray is on —
    // it does not toggle on/off each time the window is minimized/restored.
    private void RefreshTrayVisibility()
    {
        if (_trayIcon is null || _vm is null) return;
        _trayIcon.Visibility = _vm.MinimizeToTray
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        if (_vm is null || !_vm.MinimizeToTray) return;

        if (WindowState == WindowState.Minimized)
        {
            ShowInTaskbar = false;
            Hide();
            // Tray icon is already registered and visible — nothing extra needed.
        }
    }

    // Con "minimizar a bandeja" activado, pulsar la X oculta la ventana al área
    // de notificaciones en lugar de cerrar la app. El cierre real solo ocurre
    // desde el menú "Cerrar DexSuite" del icono de bandeja (_forceClose = true).
    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_vm is null || !_vm.MinimizeToTray || _forceClose) return;

        e.Cancel = true;
        ShowInTaskbar = false;
        Hide();
        // El icono de bandeja ya está visible mientras MinimizeToTray esté activo.
    }

    private void RestoreWindow()
    {
        ShowInTaskbar = true;
        if (!IsVisible) Show();
        if (WindowState != WindowState.Normal)
            WindowState = WindowState.Normal;
        Activate();
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (_vm is not null)
            _vm.PropertyChanged -= OnVmPropertyChanged;
        _trayIcon?.Dispose();
    }

    // ── Eventos de interfaz ────────────────────────────────────────────────────

    private void OnQuestionMarkPreventClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        e.Handled = true;
    }
}
