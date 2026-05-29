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

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        _vm = viewModel;

        StateChanged += OnWindowStateChanged;
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
        exitItem.Click += (_, _) => Application.Current.Shutdown();

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
