using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DexSuite.App.Converters;

/// <summary>
/// Convierte un <see cref="bool"/> a <see cref="Visibility"/> invertido:
/// <c>true</c> → Collapsed, <c>false</c> → Visible.
///
/// Usado en el selector de temas para mostrar el candado SOLO cuando
/// el item está bloqueado (IsUnlocked == false).
/// </summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && b ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility v && v == Visibility.Collapsed;
}
