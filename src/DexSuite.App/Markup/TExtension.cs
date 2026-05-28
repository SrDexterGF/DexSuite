using System.Windows.Data;
using System.Windows.Markup;
using DexSuite.App.Services;

namespace DexSuite.App.Markup;

/// <summary>
/// Markup extension para usar strings traducidas desde XAML:
/// <code>&lt;TextBlock Text="{loc:T Key=App.Tagline}"/&gt;</code>
/// o con constructor posicional:
/// <code>&lt;TextBlock Text="{loc:T App.Tagline}"/&gt;</code>
///
/// Internamente crea un Binding a <c>LocalizationService.Instance[Key]</c>,
/// por lo que cualquier cambio de idioma refresca todos los textos
/// automáticamente vía el evento PropertyChanged "Item[]".
/// </summary>
[MarkupExtensionReturnType(typeof(object))]
public sealed class TExtension : MarkupExtension
{
    public string Key { get; set; } = string.Empty;

    public TExtension() { }
    public TExtension(string key) => Key = key;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (string.IsNullOrWhiteSpace(Key))
            return "[loc:?]";

        var binding = new Binding($"[{Key}]")
        {
            Mode = BindingMode.OneWay,
            Source = LocalizationService.Instance,
        };
        return binding.ProvideValue(serviceProvider);
    }
}
