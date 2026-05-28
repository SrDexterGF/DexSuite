using System.Globalization;
using System.Windows.Data;
using DexSuite.App.Services;

namespace DexSuite.App.Converters;

/// <summary>
/// Convierte un <see cref="SecurityCheckKind"/> en su texto traducido
/// (Defender Quick scan, SFC, DISM, MRT). Se usa en el ComboBox de selección
/// y en los mensajes de estado para que el usuario vea el nombre legible.
/// </summary>
public sealed class SecurityKindConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not SecurityCheckKind kind) return string.Empty;
        return LocalizationService.Instance.Get($"Security.Kind.{kind}");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Convierte un <see cref="ChangeType"/> en su nombre técnico corto para
/// mostrarlo en la columna "Tipo" del listado de cambios revertibles.
/// </summary>
public sealed class ChangeTypeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DexSuite.App.Models.ChangeType type) return string.Empty;
        return type switch
        {
            DexSuite.App.Models.ChangeType.RegistryValue   => "Registry value",
            DexSuite.App.Models.ChangeType.RegistryKey     => "Registry key",
            DexSuite.App.Models.ChangeType.ServiceStartup  => "Service",
            DexSuite.App.Models.ChangeType.ScheduledTask   => "Scheduled task",
            DexSuite.App.Models.ChangeType.FileSystem      => "File system",
            _ => type.ToString(),
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
