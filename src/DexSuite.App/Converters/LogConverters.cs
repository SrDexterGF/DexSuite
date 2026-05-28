using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using DexSuite.App.Models;
using DexSuite.App.Services;

namespace DexSuite.App.Converters;

/// <summary>
/// Convierte <see cref="AppLogLevel"/> a un brush de acento para el badge:
/// Info=gris, Success=verde, Warning=ámbar, Error=rojo.
/// </summary>
public sealed class LogLevelToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush InfoBrush    = Frozen(Color.FromRgb(0x6B, 0x7C, 0x93));
    private static readonly SolidColorBrush SuccessBrush = Frozen(Color.FromRgb(0x00, 0xC8, 0x53));
    private static readonly SolidColorBrush WarningBrush = Frozen(Color.FromRgb(0xFF, 0xA8, 0x26));
    private static readonly SolidColorBrush ErrorBrush   = Frozen(Color.FromRgb(0xFF, 0x4D, 0x4D));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is AppLogLevel l ? l switch
        {
            AppLogLevel.Success => SuccessBrush,
            AppLogLevel.Warning => WarningBrush,
            AppLogLevel.Error   => ErrorBrush,
            _                   => InfoBrush,
        } : InfoBrush;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static SolidColorBrush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }
}

/// <summary>
/// Devuelve el texto localizado para un <see cref="AppLogLevel"/>
/// consultando la clave <c>Log.Level.&lt;Name&gt;</c>.
/// </summary>
public sealed class LogLevelToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is AppLogLevel l)
            return LocalizationService.Instance.Get($"Log.Level.{l}");
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Devuelve el texto localizado para una <see cref="AppLogCategory"/>
/// consultando la clave <c>Log.Category.&lt;Name&gt;</c>.
/// </summary>
public sealed class LogCategoryToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is AppLogCategory c)
            return LocalizationService.Instance.Get($"Log.Category.{c}");
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Convierte <see cref="DateTime"/> UTC a hora local con formato <c>dd/MM HH:mm:ss</c>.
/// </summary>
public sealed class UtcToLocalTimeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DateTime utc)
        {
            var local = utc.Kind == DateTimeKind.Utc ? utc.ToLocalTime() : utc;
            return local.ToString("dd/MM HH:mm:ss", culture);
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
