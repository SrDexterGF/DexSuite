using System.Windows;
using DexSuite.App.ViewModels;
using Wpf.Ui.Controls;

namespace DexSuite.App;

/// <summary>
/// Ventana modal de selección de juegos. Recibe el ViewModel ya construido
/// vía DI desde MainViewModel y simplemente lo asigna como DataContext.
/// </summary>
public partial class GameSelectorWindow : FluentWindow
{
    public GameSelectorWindow(GameSelectorViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e) => Close();
}
