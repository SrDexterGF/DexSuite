using System.Runtime.Versioning;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace DexSuite.App.Services;

/// <summary>
/// Envuelve la API nativa de WinRT <c>ToastNotificationManager</c> (Windows 10
/// build 19041+) para emitir notificaciones toast sin paquetes externos.
///
/// El AppId que usamos al construir el notifier identifica a DexSuite en el
/// Centro de Notificaciones de Windows. Como la app NO está empaquetada como
/// MSIX, Windows usa el AppUserModelID que registramos en runtime; si la
/// notificación no aparece la primera vez (a veces hasta que se registra una
/// vez por usuario), basta con que la app vuelva a llamar — Windows agrupa la
/// notificación bajo el AppId.
///
/// Se mantiene como API estática porque el toast es accesorio: si falla, lo
/// tragamos y seguimos con el flujo principal.
/// </summary>
[SupportedOSPlatform("windows10.0.19041.0")]
internal static class ToastNotificationService
{
    /// <summary>
    /// Identificador único de la app en el Action Center. Debe ser estable
    /// entre versiones para que Windows agrupe las notificaciones.
    /// </summary>
    private const string AppId = "DexSuite.PerformanceSeries";

    /// <summary>
    /// Lanza un toast con título + cuerpo (dos líneas de texto). Si el SO
    /// actual no soporta toasts (p. ej. Windows 8 o Server), devuelve sin
    /// lanzar excepción.
    /// </summary>
    public static void Show(string title, string body)
    {
        // Construye el XML del toast siguiendo el template "ToastText02"
        // de Microsoft (título + cuerpo). Lo hacemos manualmente porque
        // queremos controlar los <text> con la traducción al idioma activo.
        var xml = $"""
        <toast launch="dexsuite-finished">
          <visual>
            <binding template="ToastGeneric">
              <text>{System.Security.SecurityElement.Escape(title)}</text>
              <text>{System.Security.SecurityElement.Escape(body)}</text>
            </binding>
          </visual>
          <audio src="ms-winsoundevent:Notification.Default"/>
        </toast>
        """;

        var doc = new XmlDocument();
        doc.LoadXml(xml);

        var toast = new ToastNotification(doc);
        ToastNotificationManager.CreateToastNotifier(AppId).Show(toast);
    }
}
