using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using DexSuite.App.Models;
using DexSuite.App.Services;

namespace DexSuite.App.ViewModels;

/// <summary>
/// Wrapper de <see cref="ThemeDescriptor"/> con estado reactivo (IsActive,
/// IsUnlocked) para el ItemsControl del selector de temas en Ajustes.
///
/// El padre (MainViewModel) refresca estas propiedades cuando cambia el tema
/// activo o el plan del usuario, sin que cada item se suscriba a eventos.
/// </summary>
public sealed partial class ThemeItemViewModel : ObservableObject
{
    public ThemeDescriptor Descriptor { get; }

    public ThemeItemViewModel(ThemeDescriptor descriptor, bool isActive, bool isUnlocked, bool isComingSoon = false)
    {
        Descriptor = descriptor;
        this.isActive = isActive;
        this.isUnlocked = isUnlocked;
        IsComingSoon = isComingSoon;
    }

    /// <summary>True si es la tarjeta placeholder "Coming Soon": no seleccionable,
    /// se muestra deshabilitada con un badge "Próximamente".</summary>
    public bool IsComingSoon { get; }

    /// <summary>Identificador del tema (para la clave de traducción).</summary>
    public AppTheme Theme => Descriptor.Theme;

    /// <summary>Clave i18n del nombre del tema (ej. "Theme.Name.Cybernetic").</summary>
    public string NameKey => Descriptor.NameKey;

    /// <summary>Clave i18n de la descripción corta.</summary>
    public string DescriptionKey => Descriptor.DescriptionKey;

    /// <summary>Color principal de la muestra (fondo).</summary>
    public Color PreviewColor1 => Descriptor.PreviewColor1;

    /// <summary>Color secundario de la muestra (acento).</summary>
    public Color PreviewColor2 => Descriptor.PreviewColor2;

    /// <summary>Color terciario de la muestra (highlight).</summary>
    public Color PreviewColor3 => Descriptor.PreviewColor3;

    /// <summary>Plan mínimo para desbloquearlo ("Free", "Avanzado", "Pro").</summary>
    public string MinTier => Descriptor.MinTier;

    /// <summary>True si este tema es el activo en la app.</summary>
    [ObservableProperty]
    private bool isActive;

    /// <summary>True si el plan del usuario actual permite usar este tema.</summary>
    [ObservableProperty]
    private bool isUnlocked;
}
