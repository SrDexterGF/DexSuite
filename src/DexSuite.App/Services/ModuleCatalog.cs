using DexSuite.App.Models;

namespace DexSuite.App.Services;

/// <summary>
/// Catálogo de los 19 módulos de DexSuite (M20 = juegos, gestionado fuera del catálogo).
/// Distribución de tiers:
///   FREE (6)     — limpieza básica + Winget; segura y mayoritariamente reversible.
///   AVANZADO (7) — limpieza profunda y ajustes; puede afectar funcionalidades.
///   PRO (6)      — optimización extrema de rendimiento, seguridad y hardware.
///
/// Todos los textos visibles son CLAVES i18n; las traducciones reales viven
/// en scripts/modules.json y se compilan a satellite assemblies.
/// </summary>
public sealed class ModuleCatalog : IModuleCatalog
{
    private static readonly IReadOnlyList<CleanupModule> _modules = new CleanupModule[]
    {
        // ══════════════════════════════════════════════════════════════
        //  FREE — Limpieza básica (5 módulos)
        // ══════════════════════════════════════════════════════════════

        new(1,  "Modules.M01.Name", "Modules.M01.Description",
            ModuleCategory.Cleanup, ModuleTier.Free,
            RecommendedDefault: true, Reversible: true,
            SafetyReasonKey: "Modules.M01.SafetyReason",
            Impact: ImpactLevel.Soft),

        new(2,  "Modules.M02.Name", "Modules.M02.Description",
            ModuleCategory.Cleanup, ModuleTier.Free,
            RecommendedDefault: true, Reversible: true,
            SafetyReasonKey: "Modules.M02.SafetyReason",
            Impact: ImpactLevel.Soft),

        new(3,  "Modules.M03.Name", "Modules.M03.Description",
            ModuleCategory.Cleanup, ModuleTier.Free,
            RecommendedDefault: false, Reversible: false,
            SafetyReasonKey: "Modules.M03.SafetyReason",
            Impact: ImpactLevel.Soft),

        new(5,  "Modules.M05.Name", "Modules.M05.Description",
            ModuleCategory.Cleanup, ModuleTier.Free,
            RecommendedDefault: true, Reversible: true,
            SafetyReasonKey: "Modules.M05.SafetyReason",
            Impact: ImpactLevel.Soft),

        new(7,  "Modules.M07.Name", "Modules.M07.Description",
            ModuleCategory.Cleanup, ModuleTier.Free,
            RecommendedDefault: true, Reversible: true,
            SafetyReasonKey: "Modules.M07.SafetyReason",
            Impact: ImpactLevel.Soft),

        // Winget (M12): herramienta nativa de Windows, gratuita, segura y
        // reversible. Movida a Free como parte de la migración del .bat.
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
            Impact: ImpactLevel.Notable),

        new(6,  "Modules.M06.Name", "Modules.M06.Description",
            ModuleCategory.Cleanup, ModuleTier.Advanced,
            RecommendedDefault: false, Reversible: false,
            SafetyReasonKey: "Modules.M06.SafetyReason",
            Impact: ImpactLevel.Notable),

        new(8,  "Modules.M08.Name", "Modules.M08.Description",
            ModuleCategory.Cleanup, ModuleTier.Advanced,
            RecommendedDefault: false, Reversible: true,
            SafetyReasonKey: "Modules.M08.SafetyReason",
            Impact: ImpactLevel.Soft),

        new(9,  "Modules.M09.Name", "Modules.M09.Description",
            ModuleCategory.Cleanup, ModuleTier.Advanced,
            RecommendedDefault: false, Reversible: true,
            SafetyReasonKey: "Modules.M09.SafetyReason",
            Impact: ImpactLevel.Soft),

        new(10, "Modules.M10.Name", "Modules.M10.Description",
            ModuleCategory.Cleanup, ModuleTier.Advanced,
            RecommendedDefault: false, Reversible: true,
            SafetyReasonKey: "Modules.M10.SafetyReason",
            Impact: ImpactLevel.Notable),

        new(11, "Modules.M11.Name", "Modules.M11.Description",
            ModuleCategory.Settings, ModuleTier.Advanced,
            RecommendedDefault: false, Reversible: true,
            SafetyReasonKey: "Modules.M11.SafetyReason",
            Impact: ImpactLevel.Strong),

        new(13, "Modules.M13.Name", "Modules.M13.Description",
            ModuleCategory.Settings, ModuleTier.Advanced,
            RecommendedDefault: false, Reversible: true,
            SafetyReasonKey: "Modules.M13.SafetyReason",
            Impact: ImpactLevel.Strong),

        // ══════════════════════════════════════════════════════════════
        //  PRO — Optimización extrema (7 módulos)
        // ══════════════════════════════════════════════════════════════

        new(14, "Modules.M14.Name", "Modules.M14.Description",
            ModuleCategory.Settings, ModuleTier.Pro,
            RecommendedDefault: false, Reversible: true,
            SafetyReasonKey: "Modules.M14.SafetyReason",
            Impact: ImpactLevel.Strong),

        new(15, "Modules.M15.Name", "Modules.M15.Description",
            ModuleCategory.Settings, ModuleTier.Pro,
            RecommendedDefault: false, Reversible: true,
            SafetyReasonKey: "Modules.M15.SafetyReason",
            Impact: ImpactLevel.Extreme),

        new(16, "Modules.M16.Name", "Modules.M16.Description",
            ModuleCategory.Settings, ModuleTier.Pro,
            RecommendedDefault: false, Reversible: true,
            SafetyReasonKey: "Modules.M16.SafetyReason",
            Impact: ImpactLevel.Strong),

        new(17, "Modules.M17.Name", "Modules.M17.Description",
            ModuleCategory.Settings, ModuleTier.Pro,
            RecommendedDefault: false, Reversible: true,
            SafetyReasonKey: "Modules.M17.SafetyReason",
            Impact: ImpactLevel.Strong),

        new(18, "Modules.M18.Name", "Modules.M18.Description",
            ModuleCategory.Hardware, ModuleTier.Pro,
            RecommendedDefault: false, Reversible: true,
            SafetyReasonKey: "Modules.M18.SafetyReason",
            Impact: ImpactLevel.Notable),

        new(19, "Modules.M19.Name", "Modules.M19.Description",
            ModuleCategory.Hardware, ModuleTier.Pro,
            RecommendedDefault: false, Reversible: true,
            SafetyReasonKey: "Modules.M19.SafetyReason",
            Impact: ImpactLevel.Notable),

        // ══════════════════════════════════════════════════════════════
        //  NOTA: La optimización de videojuegos NO es un módulo del catálogo.
        //  Se accede desde la ventana modal GameSelectorWindow en la sección
        //  EXTRA PRO; el lanzador WPF ejecuta directamente el .ps1 del juego
        //  elegido (repo Game_Configs).
        // ══════════════════════════════════════════════════════════════
    };

    public IReadOnlyList<CleanupModule> GetAll() => _modules;
}
