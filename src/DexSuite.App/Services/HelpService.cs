using System.Windows;
using UiMessageBox = Wpf.Ui.Controls.MessageBox;

namespace DexSuite.App.Services;

/// <summary>
/// Implementación de <see cref="IHelpService"/> usando el MessageBox Fluent
/// de Wpf.Ui. Es un stub funcional; en versiones posteriores se reemplazará
/// por un panel/popup con mejor formato (markdown, links, imágenes).
/// </summary>
public sealed class HelpService : IHelpService
{
    private readonly ILocalizationService _loc;

    public HelpService(ILocalizationService loc) => _loc = loc;

    public async Task ShowHelpAsync(string titleKey, string descriptionKey)
    {
        var box = new UiMessageBox
        {
            Title = _loc.Get(titleKey),
            Content = _loc.Get(descriptionKey),
            CloseButtonText = _loc.Get("Common.Close"),
        };
        // El MessageBox de Wpf.Ui necesita correr en el hilo de UI.
        await Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            await box.ShowDialogAsync();
        });
    }
}
