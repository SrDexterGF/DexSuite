using System.Windows.Media;
using DexSuite.App.Models;

namespace DexSuite.App.Services;

/// <summary>
/// Servicio que gestiona el tema visual de la aplicación.
/// Aplica un ResourceDictionary distinto en runtime para cambiar la paleta
/// sin reiniciar la app.
/// </summary>
public interface IThemeService
{
    /// <summary>Tema actualmente aplicado.</summary>
    AppTheme CurrentTheme { get; }

    /// <summary>Catálogo de descripciones de tema (icono, nombre, mínimo plan).</summary>
    IReadOnlyList<ThemeDescriptor> AvailableThemes { get; }

    /// <summary>Cambia el tema activo y lo aplica al ResourceDictionary global.</summary>
    void ApplyTheme(AppTheme theme);

    /// <summary>
    /// Carga el tema persistido (o devuelve <see cref="AppTheme.Default"/> si no hay).
    /// Llamar una vez al arrancar la aplicación.
    /// </summary>
    AppTheme LoadPersistedTheme();

    /// <summary>Persiste el tema actual a disco.</summary>
    Task PersistAsync();

    /// <summary>Se dispara cada vez que cambia el tema activo.</summary>
    event EventHandler<AppTheme>? ThemeChanged;
}

/// <summary>
/// Descriptor de un tema: metadatos para mostrarlo en el selector
/// (nombre vía clave i18n, colores de preview y plan mínimo).
/// </summary>
/// <param name="Theme">Identificador del tema.</param>
/// <param name="NameKey">Clave i18n para el nombre visible ("Theme.Name.Default", etc.).</param>
/// <param name="DescriptionKey">Clave i18n para la descripción corta ("Theme.Desc.Default", etc.).</param>
/// <param name="PreviewColor1">Color principal de fondo en la muestra.</param>
/// <param name="PreviewColor2">Color secundario (acento) en la muestra.</param>
/// <param name="PreviewColor3">Color terciario (highlight) en la muestra.</param>
/// <param name="MinTier">Plan mínimo requerido para desbloquear el tema.</param>
public sealed record ThemeDescriptor(
    AppTheme Theme,
    string NameKey,
    string DescriptionKey,
    Color PreviewColor1,
    Color PreviewColor2,
    Color PreviewColor3,
    string MinTier);
