using DexSuite.App.Models;

namespace DexSuite.App.Services;

/// <summary>
/// Catálogo de los 19 módulos de DexSuite (M20 = juegos, gestionado fuera del catálogo).
/// Distribución de tiers:
///   FREE (6)     — limpieza básica + Winget; segura y mayoritariamente reversible.
///   AVANZADO (7) — limpieza profunda y ajustes; puede afectar funcionalidades.
///   PRO (6)      — optimización extrema de rendimiento, seguridad y hardware.
///
/// Cada módulo declara además sus SUB-OPCIONES atómicas: en la vista avanzada de
/// Módulos el usuario las activa/desactiva por separado, de modo que ningún clic
/// aplica más de un cambio a la vez. El Id de cada sub-opción ("M13_cortana") debe
/// coincidir exactamente con el que comprueba el executor vía Want(...).
///
/// Todos los textos visibles son CLAVES i18n; las traducciones reales viven
/// en los .resx (Modules.Sub.{subId}.Name / .Desc).
/// </summary>
public sealed class ModuleCatalog : IModuleCatalog
{
    /// <summary>Atajo: crea una sub-opción con sus claves i18n derivadas del Id.</summary>
    private static ModuleSubOption So(string id)
        => new(id, $"Modules.Sub.{id}.Name", $"Modules.Sub.{id}.Desc");

    private static readonly IReadOnlyList<CleanupModule> _modules = new CleanupModule[]
    {
        // ══════════════════════════════════════════════════════════════
        //  FREE — Limpieza básica (5 módulos)
        // ══════════════════════════════════════════════════════════════

        new(1,  "Modules.M01.Name", "Modules.M01.Description",
            ModuleCategory.Cleanup, ModuleTier.Free,
            RecommendedDefault: true, Reversible: true,
            SafetyReasonKey: "Modules.M01.SafetyReason",
            Impact: ImpactLevel.Soft,
            SubOptions: new[]
            {
                So("M01_prefetch"), So("M01_win_temp"), So("M01_user_temp"),
                So("M01_thumb_icon"), So("M01_d3ds"),
            }),

        new(2,  "Modules.M02.Name", "Modules.M02.Description",
            ModuleCategory.Cleanup, ModuleTier.Free,
            RecommendedDefault: true, Reversible: true,
            SafetyReasonKey: "Modules.M02.SafetyReason",
            Impact: ImpactLevel.Soft,
            SubOptions: new[]
            {
                So("M02_logs_root"), So("M02_logs_sys32"), So("M02_logs_softwdist"),
                So("M02_logs_inf"), So("M02_logs_cbs"), So("M02_wer"),
            }),

        new(3,  "Modules.M03.Name", "Modules.M03.Description",
            ModuleCategory.Cleanup, ModuleTier.Free,
            RecommendedDefault: false, Reversible: false,
            SafetyReasonKey: "Modules.M03.SafetyReason",
            Impact: ImpactLevel.Soft,
            SubOptions: new[]
            {
                So("M03_temp_aggressive"), So("M03_recent"), So("M03_recycle"), So("M03_windows_old"),
            }),

        new(5,  "Modules.M05.Name", "Modules.M05.Description",
            ModuleCategory.Cleanup, ModuleTier.Free,
            RecommendedDefault: true, Reversible: true,
            SafetyReasonKey: "Modules.M05.SafetyReason",
            Impact: ImpactLevel.Soft,
            SubOptions: new[]
            {
                So("M05_stop_services"), So("M05_cache_dl"), So("M05_restart_services"), So("M05_delivery_opt"),
            }),

        new(7,  "Modules.M07.Name", "Modules.M07.Description",
            ModuleCategory.Cleanup, ModuleTier.Free,
            RecommendedDefault: true, Reversible: true,
            SafetyReasonKey: "Modules.M07.SafetyReason",
            Impact: ImpactLevel.Soft,
            SubOptions: new[]
            {
                So("M07_edge"), So("M07_chrome"), So("M07_firefox"),
                So("M07_brave"), So("M07_opera"), So("M07_vivaldi"),
            }),

        // Winget (M12): herramienta nativa de Windows, gratuita, segura y
        // reversible. Acción única (sin sub-opciones).
        new(12, "Modules.M12.Name", "Modules.M12.Description",
            ModuleCategory.Settings, ModuleTier.Free,
            RecommendedDefault: false, Reversible: false,
            SafetyReasonKey: "Modules.M12.SafetyReason",
            Impact: ImpactLevel.Soft),

        // ══════════════════════════════════════════════════════════════
        //  AVANZADO — Limpieza profunda + tweaks (7 módulos)
        // ══════════════════════════════════════════════════════════════

        new(4,  "Modules.M04.Name", "Modules.M04.Description",
            ModuleCategory.Cleanup, ModuleTier.Advanced,
            RecommendedDefault: false, Reversible: false,
            SafetyReasonKey: "Modules.M04.SafetyReason",
            Impact: ImpactLevel.Notable,
            SubOptions: new[]
            {
                So("M04_crash_dumps"), So("M04_spooler"), So("M04_wmi"), So("M04_event_log"),
            }),

        new(6,  "Modules.M06.Name", "Modules.M06.Description",
            ModuleCategory.Cleanup, ModuleTier.Advanced,
            RecommendedDefault: false, Reversible: false,
            SafetyReasonKey: "Modules.M06.SafetyReason",
            Impact: ImpactLevel.Notable,
            SubOptions: new[] { So("M06_analyze"), So("M06_cleanup") }),

        new(8,  "Modules.M08.Name", "Modules.M08.Description",
            ModuleCategory.Cleanup, ModuleTier.Advanced,
            RecommendedDefault: false, Reversible: true,
            SafetyReasonKey: "Modules.M08.SafetyReason",
            Impact: ImpactLevel.Soft,
            SubOptions: new[] { So("M08_dns_flush"), So("M08_dns_register"), So("M08_gpupdate") }),

        new(9,  "Modules.M09.Name", "Modules.M09.Description",
            ModuleCategory.Cleanup, ModuleTier.Advanced,
            RecommendedDefault: false, Reversible: true,
            SafetyReasonKey: "Modules.M09.SafetyReason",
            Impact: ImpactLevel.Soft,
            SubOptions: new[] { So("M09_store"), So("M09_onedrive"), So("M09_teams") }),

        new(10, "Modules.M10.Name", "Modules.M10.Description",
            ModuleCategory.Cleanup, ModuleTier.Advanced,
            RecommendedDefault: false, Reversible: true,
            SafetyReasonKey: "Modules.M10.SafetyReason",
            Impact: ImpactLevel.Notable,
            SubOptions: new[] { So("M10_sfc"), So("M10_dism") }),

        new(11, "Modules.M11.Name", "Modules.M11.Description",
            ModuleCategory.Settings, ModuleTier.Advanced,
            RecommendedDefault: false, Reversible: true,
            SafetyReasonKey: "Modules.M11.SafetyReason",
            Impact: ImpactLevel.Strong,
            SubOptions: new[]
            {
                So("M11_mouse_accel"), So("M11_double_click"), So("M11_keyboard"),
                So("M11_monitor_hz"), So("M11_mouse_curve"),
            }),

        new(13, "Modules.M13.Name", "Modules.M13.Description",
            ModuleCategory.Settings, ModuleTier.Advanced,
            RecommendedDefault: false, Reversible: true,
            SafetyReasonKey: "Modules.M13.SafetyReason",
            Impact: ImpactLevel.Strong,
            SubOptions: new[]
            {
                So("M13_copilot"), So("M13_cortana"), So("M13_telemetry"),
                So("M13_telemetry_svc"), So("M13_timeline"),
            }),

        // ══════════════════════════════════════════════════════════════
        //  PRO — Optimización extrema (7 módulos)
        // ══════════════════════════════════════════════════════════════

        new(14, "Modules.M14.Name", "Modules.M14.Description",
            ModuleCategory.Settings, ModuleTier.Pro,
            RecommendedDefault: false, Reversible: true,
            SafetyReasonKey: "Modules.M14.SafetyReason",
            Impact: ImpactLevel.Strong,
            SubOptions: new[]
            {
                So("M14_silent_apps"), So("M14_location"), So("M14_widgets"),
                So("M14_store_search"), So("M14_wpbt"), So("M14_services"),
                So("M14_svchost"), So("M14_ps7_telemetry"), So("M14_sticky_keys"),
            }),

        new(15, "Modules.M15.Name", "Modules.M15.Description",
            ModuleCategory.Settings, ModuleTier.Pro,
            RecommendedDefault: false, Reversible: true,
            SafetyReasonKey: "Modules.M15.SafetyReason",
            Impact: ImpactLevel.Extreme,
            SubOptions: new[]
            {
                So("M15_power_throttle"), So("M15_mmcss"), So("M15_gamemode"), So("M15_fse"),
                So("M15_timeouts"), So("M15_maintenance"), So("M15_hibernate"), So("M15_sync"),
                So("M15_transparency"), So("M15_bg_apps"), So("M15_input_telemetry"),
                So("M15_hags"), So("M15_diag_svc"), So("M15_bcd"),
            }),

        new(16, "Modules.M16.Name", "Modules.M16.Description",
            ModuleCategory.Settings, ModuleTier.Pro,
            RecommendedDefault: false, Reversible: true,
            SafetyReasonKey: "Modules.M16.SafetyReason",
            Impact: ImpactLevel.Strong,
            SubOptions: new[]
            {
                So("M16_nic"), So("M16_tcp_global"), So("M16_tcp_nagle"), So("M16_ip_stack"),
                So("M16_qos"), So("M16_dns"), So("M16_cache_clear"),
            }),

        new(17, "Modules.M17.Name", "Modules.M17.Description",
            ModuleCategory.Settings, ModuleTier.Pro,
            RecommendedDefault: false, Reversible: true,
            SafetyReasonKey: "Modules.M17.SafetyReason",
            Impact: ImpactLevel.Strong,
            SubOptions: new[]
            {
                So("M17_mrt"), So("M17_defender_sig"), So("M17_defender_scan"), So("M17_smbv1"),
                So("M17_firewall"), So("M17_autorun"), So("M17_dep"), So("M17_llmnr"),
                So("M17_netbios"), So("M17_pua"), So("M17_rdp"), So("M17_uac"),
            }),

        new(18, "Modules.M18.Name", "Modules.M18.Description",
            ModuleCategory.Hardware, ModuleTier.Pro,
            RecommendedDefault: false, Reversible: true,
            SafetyReasonKey: "Modules.M18.SafetyReason",
            Impact: ImpactLevel.Notable,
            SubOptions: new[] { So("M18_trim_enable"), So("M18_trim_smart") }),

        new(19, "Modules.M19.Name", "Modules.M19.Description",
            ModuleCategory.Hardware, ModuleTier.Pro,
            RecommendedDefault: false, Reversible: true,
            SafetyReasonKey: "Modules.M19.SafetyReason",
            Impact: ImpactLevel.Notable,
            SubOptions: new[]
            {
                So("M19_scan_devices"), So("M19_enum_drivers"), So("M19_wu_drivers"), So("M19_reminder"),
            }),

        // ══════════════════════════════════════════════════════════════
        //  NOTA: La optimización de videojuegos NO es un módulo del catálogo.
        //  Se accede desde la ventana modal GameSelectorWindow en la sección
        //  EXTRA PRO; el lanzador WPF ejecuta directamente el .ps1 del juego
        //  elegido (repo Game_Configs).
        // ══════════════════════════════════════════════════════════════
    };

    public IReadOnlyList<CleanupModule> GetAll() => _modules;
}
