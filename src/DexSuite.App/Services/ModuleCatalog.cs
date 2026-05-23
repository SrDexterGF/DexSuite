using DexSuite.App.Models;

namespace DexSuite.App.Services;

/// <summary>
/// Inventario de los 20 módulos del .bat DexSuite_CleanUp_v*.bat.
/// El Id corresponde al número del menú manual del .bat.
///
/// Todos los textos visibles son CLAVES i18n; las traducciones reales viven
/// en <c>scripts/modules.json</c> y se compilan a satellite assemblies.
/// </summary>
public sealed class ModuleCatalog : IModuleCatalog
{
    private static readonly IReadOnlyList<CleanupModule> _modules = new CleanupModule[]
    {
        // ----- LIMPIEZA Y MANTENIMIENTO -----
        new(1,  "Modules.M01.Name", "Modules.M01.Description",
            ModuleCategory.Cleanup, ModuleTier.Free, RecommendedDefault: true, Reversible: true,
            SafetyReasonKey: "Modules.M01.SafetyReason"),

        new(2,  "Modules.M02.Name", "Modules.M02.Description",
            ModuleCategory.Cleanup, ModuleTier.Free, RecommendedDefault: true, Reversible: true,
            SafetyReasonKey: "Modules.M02.SafetyReason"),

        new(3,  "Modules.M03.Name", "Modules.M03.Description",
            ModuleCategory.Cleanup, ModuleTier.Free, RecommendedDefault: true, Reversible: true,
            SafetyReasonKey: "Modules.M03.SafetyReason"),

        new(4,  "Modules.M04.Name", "Modules.M04.Description",
            ModuleCategory.Cleanup, ModuleTier.Free, RecommendedDefault: false, Reversible: false,
            SafetyReasonKey: "Modules.M04.SafetyReason"),

        new(5,  "Modules.M05.Name", "Modules.M05.Description",
            ModuleCategory.Cleanup, ModuleTier.Free, RecommendedDefault: true, Reversible: true,
            SafetyReasonKey: "Modules.M05.SafetyReason"),

        new(6,  "Modules.M06.Name", "Modules.M06.Description",
            ModuleCategory.Cleanup, ModuleTier.Advanced, RecommendedDefault: false, Reversible: false,
            SafetyReasonKey: "Modules.M06.SafetyReason"),

        new(7,  "Modules.M07.Name", "Modules.M07.Description",
            ModuleCategory.Cleanup, ModuleTier.Free, RecommendedDefault: true, Reversible: true,
            SafetyReasonKey: "Modules.M07.SafetyReason"),

        new(8,  "Modules.M08.Name", "Modules.M08.Description",
            ModuleCategory.Cleanup, ModuleTier.Free, RecommendedDefault: true, Reversible: true,
            SafetyReasonKey: "Modules.M08.SafetyReason"),

        new(9,  "Modules.M09.Name", "Modules.M09.Description",
            ModuleCategory.Cleanup, ModuleTier.Free, RecommendedDefault: true, Reversible: true,
            SafetyReasonKey: "Modules.M09.SafetyReason"),

        new(10, "Modules.M10.Name", "Modules.M10.Description",
            ModuleCategory.Cleanup, ModuleTier.Advanced, RecommendedDefault: false, Reversible: true,
            SafetyReasonKey: "Modules.M10.SafetyReason"),

        // ----- AJUSTES, RENDIMIENTO Y SEGURIDAD -----
        new(11, "Modules.M11.Name", "Modules.M11.Description",
            ModuleCategory.Settings, ModuleTier.Advanced, RecommendedDefault: false, Reversible: true,
            SafetyReasonKey: "Modules.M11.SafetyReason"),

        new(12, "Modules.M12.Name", "Modules.M12.Description",
            ModuleCategory.Settings, ModuleTier.Advanced, RecommendedDefault: false, Reversible: false,
            SafetyReasonKey: "Modules.M12.SafetyReason"),

        new(13, "Modules.M13.Name", "Modules.M13.Description",
            ModuleCategory.Settings, ModuleTier.Advanced, RecommendedDefault: false, Reversible: true,
            SafetyReasonKey: "Modules.M13.SafetyReason"),

        new(14, "Modules.M14.Name", "Modules.M14.Description",
            ModuleCategory.Settings, ModuleTier.Pro, RecommendedDefault: false, Reversible: true,
            SafetyReasonKey: "Modules.M14.SafetyReason"),

        new(15, "Modules.M15.Name", "Modules.M15.Description",
            ModuleCategory.Settings, ModuleTier.Pro, RecommendedDefault: false, Reversible: true,
            SafetyReasonKey: "Modules.M15.SafetyReason"),

        new(16, "Modules.M16.Name", "Modules.M16.Description",
            ModuleCategory.Settings, ModuleTier.Pro, RecommendedDefault: false, Reversible: true,
            SafetyReasonKey: "Modules.M16.SafetyReason"),

        new(17, "Modules.M17.Name", "Modules.M17.Description",
            ModuleCategory.Settings, ModuleTier.Pro, RecommendedDefault: false, Reversible: true,
            SafetyReasonKey: "Modules.M17.SafetyReason"),

        // ----- HARDWARE -----
        new(18, "Modules.M18.Name", "Modules.M18.Description",
            ModuleCategory.Hardware, ModuleTier.Free, RecommendedDefault: true, Reversible: true,
            SafetyReasonKey: "Modules.M18.SafetyReason"),

        new(19, "Modules.M19.Name", "Modules.M19.Description",
            ModuleCategory.Hardware, ModuleTier.Free, RecommendedDefault: true, Reversible: true,
            SafetyReasonKey: "Modules.M19.SafetyReason"),

        // ----- EXTRAS -----
        new(20, "Modules.M20.Name", "Modules.M20.Description",
            ModuleCategory.Extras, ModuleTier.Pro, RecommendedDefault: false, Reversible: false,
            SafetyReasonKey: "Modules.M20.SafetyReason"),
    };

    public IReadOnlyList<CleanupModule> GetAll() => _modules;
}
