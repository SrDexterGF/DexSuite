using DexSuite.App.ViewModels;
using Wpf.Ui.Controls;

namespace DexSuite.App;

public partial class MainWindow : FluentWindow
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
