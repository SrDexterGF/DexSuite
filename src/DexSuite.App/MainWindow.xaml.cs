using DexSuite.App.ViewModels;
using H.NotifyIcon;
using System.Windows;
using Wpf.Ui.Controls;
using WpfMenuItem  = System.Windows.Controls.MenuItem;
using WpfSeparator = System.Windows.Controls.Separator;
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
        // Retrieve the TaskbarIcon declared in XAML resources so that H.NotifyIcon
        // registers it properly with the WPF logical tree before any state changes.
        Loaded += (_, _) => InitTrayIcon();
    }

    // ── Bandeja del sistema ────────────────────────────────────────────────────

    private void InitTrayIcon()
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

        _trayIcon.ContextMenu = menu;
        _trayIcon.TrayMouseDoubleClick += (_, _) => RestoreWindow();
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        if (_vm is null || !_vm.MinimizeToTray) return;

        if (WindowState == WindowState.Minimized)
        {
            ShowInTaskbar = false;
            Hide();
            if (_trayIcon is not null)
                _trayIcon.Visibility = Visibility.Visible;
        }
    }

    private void RestoreWindow()
    {
        if (_trayIcon is not null)
            _trayIcon.Visibility = Visibility.Collapsed;
        ShowInTaskbar = true;
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _trayIcon?.Dispose();
    }

    // ── Evento de título ───────────────────────────────────────────────────────

    private void OnQuestionMarkPreventClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        e.Handled = true;
    }
}
