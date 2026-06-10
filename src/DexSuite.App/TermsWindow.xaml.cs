using System.Windows;
using Wpf.Ui.Controls;

namespace DexSuite.App;

public partial class TermsWindow : FluentWindow
{
    /// <summary>True si el usuario aceptó los términos; false si declinó o cerró la ventana.</summary>
    public bool Accepted { get; private set; }

    /// <param name="readOnly">true = modo informativo desde Ajustes: oculta aceptar/declinar
    /// y muestra solo "Cerrar". No altera ningún flag de aceptación.</param>
    public TermsWindow(bool readOnly = false)
    {
        InitializeComponent();
        if (readOnly)
        {
            AcceptNote.Visibility    = Visibility.Collapsed;
            AcceptButtons.Visibility = Visibility.Collapsed;
            CloseButton.Visibility   = Visibility.Visible;
        }
    }

    private void OnAcceptClicked(object sender, RoutedEventArgs e)
    {
        Accepted = true;
        Close();
    }

    private void OnDeclineClicked(object sender, RoutedEventArgs e)
    {
        Accepted = false;
        Close();
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e) => Close();
}
