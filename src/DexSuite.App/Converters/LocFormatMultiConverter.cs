using System.Globalization;
using System.Windows.Data;
using DexSuite.App.Services;

namespace DexSuite.App.Converters;

/// <summary>
/// MultiValueConverter que formatea N valores con un patrón localizado.
/// La clave i18n se pasa como <c>ConverterParameter</c>:
///
/// <code>
/// &lt;MultiBinding Converter="{StaticResource LocFormat}" ConverterParameter="Specs.CoresLabel"&gt;
///     &lt;Binding Path="Cores"/&gt;
///     &lt;Binding Path="Logical"/&gt;
/// &lt;/MultiBinding&gt;
/// </code>
///
/// Existe porque <c>MultiBinding.StringFormat</c> es una propiedad CLR (no
/// una DependencyProperty), y la markup extension <c>{loc:T}</c> devuelve un
/// Binding que sólo se puede asignar a DPs. Este converter resuelve la clave
/// y formatea el resultado sin pasar por StringFormat.
/// </summary>
public sealed class LocFormatMultiConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (parameter is not string key || string.IsNullOrWhiteSpace(key))
            return string.Empty;
        var format = LocalizationService.Instance.Get(key);
        try
        {
            return string.Format(culture, format, values);
        }
        catch
        {
            // Si el format-string tiene placeholders que no encajan con los
            // valores, devolvemos el texto crudo para no tirar el binding.
            return format;
        }
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
