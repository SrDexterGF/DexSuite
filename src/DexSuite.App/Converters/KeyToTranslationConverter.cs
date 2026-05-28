using System.Globalization;
using System.Windows.Data;
using DexSuite.App.Services;

namespace DexSuite.App.Converters;

/// <summary>
/// Converter que traduce una clave i18n al string del idioma activo.
///
/// Útil para bindings donde el ValueSource emite una clave (p. ej. el
/// veredicto del PerformanceScore) en lugar del texto final.
/// Uso: <code>{Binding ScoreBefore.Verdict, Converter={StaticResource Loc}}</code>
/// </summary>
public sealed class KeyToTranslationConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value as string;
        if (string.IsNullOrEmpty(key)) return string.Empty;
        return LocalizationService.Instance.Get(key);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
