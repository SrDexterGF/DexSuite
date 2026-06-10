namespace DexSuite.App.Models;

/// <summary>
/// Una sub-operación atómica dentro de un módulo. En la vista avanzada de
/// Módulos cada sub-opción se muestra como un checkbox independiente, de modo
/// que el usuario puede aplicar exactamente los ajustes que quiera sin que un
/// solo clic dispare varios cambios a la vez.
///
/// El <see cref="Id"/> es estable (p. ej. "M13_cortana") y es lo que el
/// executor usa para decidir si ejecuta ese bloque. Los *Key son claves i18n.
/// </summary>
/// <param name="Id">Identificador estable de la sub-operación (único dentro del módulo).</param>
/// <param name="NameKey">Clave i18n del nombre corto de la sub-opción.</param>
/// <param name="DescriptionKey">Clave i18n de la explicación de qué hace.</param>
public sealed record ModuleSubOption(
    string Id,
    string NameKey,
    string DescriptionKey);
