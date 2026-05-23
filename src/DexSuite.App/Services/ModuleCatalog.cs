using DexSuite.App.Models;

namespace DexSuite.App.Services;

/// <summary>
/// Inventario hardcodeado de los 20 módulos del .bat DexSuite_CleanUp_v*.bat.
/// El Id corresponde al número del menú manual del .bat.
/// </summary>
public sealed class ModuleCatalog : IModuleCatalog
{
    private static readonly IReadOnlyList<CleanupModule> _modules = new CleanupModule[]
    {
        // ----- LIMPIEZA Y MANTENIMIENTO -----
        new(1,  "Prefetch, caché y D3DSCache",
            "Borra Prefetch, Temp del sistema y del usuario, miniaturas, IconCache y shaders DirectX.\nTodo se regenera solo.",
            ModuleCategory.Cleanup, ModuleTier.Free, RecommendedDefault: true, Reversible: true,
            SafetyReason: "Solo borra cachés que Windows regenera automáticamente al usar el equipo.\nNo afecta a tus datos ni programas."),

        new(2,  "Logs del sistema",
            "Limpia logs antiguos de Windows, SoftwareDistribution, Windows Update, CBS, DISM y la carpeta WER.",
            ModuleCategory.Cleanup, ModuleTier.Free, RecommendedDefault: true, Reversible: true,
            SafetyReason: "Solo borra archivos de log diagnósticos.\nNo afecta al funcionamiento ni a datos personales."),

        new(3,  "Temporales, recientes y papelera",
            "Vacía las carpetas Temp resistentes, el historial reciente, la papelera y Windows.old si existe.",
            ModuleCategory.Cleanup, ModuleTier.Free, RecommendedDefault: true, Reversible: true,
            SafetyReason: "Vacía la papelera y los archivos temporales.\nSi habías enviado algo importante a la papelera, recupéralo antes."),

        new(4,  "Limpieza profunda",
            "Crash dumps, cola de impresión, compactación de WMI y vaciado del Visor de Eventos.\nLos eventos no son recuperables.",
            ModuleCategory.Cleanup, ModuleTier.Free, RecommendedDefault: false, Reversible: false,
            SafetyReason: "Vacía el Visor de Eventos: una vez borrados los eventos del sistema, no se pueden recuperar.\nÚtil para privacidad, pero piénsalo antes."),

        new(5,  "Caché de Windows Update",
            "Detiene wuauserv y BITS, borra SoftwareDistribution\\Download y Delivery Optimization, y reinicia los servicios.",
            ModuleCategory.Cleanup, ModuleTier.Free, RecommendedDefault: true, Reversible: true,
            SafetyReason: "Solo vacía la caché de descargas de Windows Update.\nSi una actualización había fallado, este módulo la arregla."),

        new(6,  "DISM Component Store",
            "Limpia componentes obsoletos del sistema.\nTarda 5-20 minutos y es irreversible (no podrás desinstalar actualizaciones previas).",
            ModuleCategory.Cleanup, ModuleTier.Advanced, RecommendedDefault: false, Reversible: false,
            SafetyReason: "Tras esta limpieza ya no podrás desinstalar las actualizaciones de Windows que ya tenías aplicadas.\nEs seguro, pero permanente."),

        new(7,  "Caché de navegadores",
            "Vacía la caché de Edge, Chrome, Firefox, Brave, Opera y Vivaldi.\nNo toca perfiles ni contraseñas.",
            ModuleCategory.Cleanup, ModuleTier.Free, RecommendedDefault: true, Reversible: true,
            SafetyReason: "Solo vacía la caché de los navegadores.\nNO toca contraseñas, marcadores, historial ni cookies de sesión."),

        new(8,  "Red (DNS y gpupdate)",
            "Vacía la caché DNS, registra el equipo en DNS y fuerza la actualización de directivas de grupo.",
            ModuleCategory.Cleanup, ModuleTier.Free, RecommendedDefault: true, Reversible: true,
            SafetyReason: "Solo refresca la caché DNS y las directivas de grupo.\nNo cambia la configuración de red persistente."),

        new(9,  "Store, OneDrive y Teams",
            "Resetea la caché de Microsoft Store, vacía los logs de OneDrive y limpia la caché de Teams.",
            ModuleCategory.Cleanup, ModuleTier.Free, RecommendedDefault: true, Reversible: true,
            SafetyReason: "Limpia las cachés de Microsoft Store, OneDrive y Teams.\nTus archivos sincronizados y conversaciones se conservan."),

        new(10, "SFC + DISM (reparación)",
            "Comprueba los archivos protegidos del sistema y repara la imagen de Windows.\nReparativo, no destructivo.",
            ModuleCategory.Cleanup, ModuleTier.Advanced, RecommendedDefault: false, Reversible: true,
            SafetyReason: "Repara archivos del sistema dañados.\nEs lento (10-30 minutos), pero NO modifica datos del usuario."),

        // ----- AJUSTES, RENDIMIENTO Y SEGURIDAD -----
        new(11, "Ratón, teclado y monitores",
            "Desactiva la aceleración del ratón, pone el teclado a máximo, desactiva sticky keys y lee los Hz máximos de los monitores.",
            ModuleCategory.Settings, ModuleTier.Advanced, RecommendedDefault: false, Reversible: true,
            SafetyReason: "Toca preferencias de ratón y teclado en el registro.\nSi no te gustan los cambios, se pueden revertir desde Configuración de Windows."),

        new(12, "Winget (actualizar apps)",
            "Lanza 'winget upgrade --all'.\nPuede tardar según el número de apps. Sin undo por módulo.",
            ModuleCategory.Settings, ModuleTier.Advanced, RecommendedDefault: false, Reversible: false,
            SafetyReason: "Actualiza TODAS tus apps a la última versión.\nSi una versión nueva trae un cambio que no te gusta, no se puede deshacer desde aquí."),

        new(13, "Copilot, Cortana y telemetría",
            "Desactiva Windows Copilot, Cortana, DiagTrack, Timeline, AdvertisingID y CEIP.",
            ModuleCategory.Settings, ModuleTier.Advanced, RecommendedDefault: false, Reversible: true,
            SafetyReason: "Cambia la configuración del registro para desactivar Copilot, Cortana y la telemetría.\nReversible volviendo a activar cada opción."),

        new(14, "Privacidad y servicios",
            "Bloquea apps sugeridas, ubicación, widgets, WPBT y desactiva servicios poco útiles.",
            ModuleCategory.Settings, ModuleTier.Pro, RecommendedDefault: false, Reversible: true,
            SafetyReason: "Modifica políticas de privacidad y deshabilita varios servicios de Windows.\nReversible, pero afecta a funciones como Widgets o ubicación."),

        new(15, "Rendimiento y latencia",
            "Power Throttling off, MMCSS prioritario, hibernación off, HAGS on, BCD platform tick (necesita reinicio).",
            ModuleCategory.Settings, ModuleTier.Pro, RecommendedDefault: false, Reversible: true,
            SafetyReason: "Cambia BCD, MMCSS, hibernación y HAGS.\nNecesita reinicio. Son cambios fuertes pero reversibles."),

        new(16, "Ethernet (optimización de red)",
            "Tunea NIC (RSS, offload, EEE off), stack TCP (Nagle off, MaxUserPort 65534) y DNS Google/Cloudflare.",
            ModuleCategory.Settings, ModuleTier.Pro, RecommendedDefault: false, Reversible: true,
            SafetyReason: "Modifica parámetros del adaptador de red y el stack TCP.\nSi tras esto pierdes conexión, ejecuta 'netsh int ip reset'."),

        new(17, "Seguridad",
            "MRT, Defender quick scan, SMBv1 off, AutoRun off, DEP AlwaysOn, LLMNR/NetBIOS off, RDP off, UAC nivel 2.",
            ModuleCategory.Settings, ModuleTier.Pro, RecommendedDefault: false, Reversible: true,
            SafetyReason: "Endurece la seguridad del sistema.\nDesactiva RDP: si lo usabas para conectarte en remoto, actívalo de nuevo manualmente."),

        // ----- HARDWARE -----
        new(18, "SSD (TRIM y salud SMART)",
            "Asegura que TRIM esté activo en NTFS/ReFS, lanza TRIM en los SSDs detectados y lee temperatura y desgaste SMART.",
            ModuleCategory.Hardware, ModuleTier.Free, RecommendedDefault: true, Reversible: true,
            SafetyReason: "Solo lee el estado SMART y lanza TRIM (operación estándar de SSD).\nNo modifica datos."),

        new(19, "Drivers (Windows Update + pnputil)",
            "pnputil scan, lista los drivers OEM y lanza Windows Update para buscar drivers nuevos.",
            ModuleCategory.Hardware, ModuleTier.Free, RecommendedDefault: true, Reversible: true,
            SafetyReason: "Solo busca drivers.\nNO los instala automáticamente: tú decides si actualizarlos desde Windows Update."),

        // ----- EXTRAS -----
        new(20, "Optimización de videojuegos",
            "Descarga y ejecuta un menú remoto (PowerShell desde GitHub) con optimizaciones por juego.",
            ModuleCategory.Extras, ModuleTier.Pro, RecommendedDefault: false, Reversible: false,
            SafetyReason: "Descarga y ejecuta código desde GitHub en tu PC.\nSolo actívalo si confías en la fuente (Sr. Dexter / Game_Configs)."),
    };

    public IReadOnlyList<CleanupModule> GetAll() => _modules;
}
