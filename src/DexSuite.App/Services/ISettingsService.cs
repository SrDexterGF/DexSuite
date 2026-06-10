namespace DexSuite.App.Services;

/// <summary>
/// Servicio que persiste las preferencias de usuario entre sesiones.
/// Guarda en %LocalAppData%/DexSuite/settings.json.
///
/// Patrón: el ViewModel llama a <see cref="ScheduleSave"/> cuando cambia una
/// propiedad relevante. El servicio agrupa cambios en una ventana corta y
/// escribe una sola vez al disco (para no machacar el archivo con cada toggle).
/// </summary>
public interface ISettingsService
{
    /// <summary>Carga las preferencias persistidas. Devuelve valores por defecto si no hay archivo o está corrupto.</summary>
    AppSettings Load();

    /// <summary>
    /// Marca el snapshot dado como pendiente de guardar. Si se llama varias
    /// veces seguidas, sólo se escribe la última versión tras un breve debounce.
    /// </summary>
    void ScheduleSave(AppSettings settings);

    /// <summary>Fuerza un guardado inmediato (p.ej. al cerrar la app).</summary>
    Task FlushAsync();
}

/// <summary>
/// Snapshot serializable de todas las preferencias persistibles.
/// Cualquier propiedad nueva debe añadirse aquí con un valor por defecto seguro.
/// </summary>
public sealed class AppSettings
{
    public string Language { get; set; } = "es";
    public string UpdateChannel { get; set; } = "Stable";

    public bool AutoSelectRecommended { get; set; } = false;
    public bool JumpToLogOnRun { get; set; } = false;

    // Vista de módulos: false = simple (agrupada), true = avanzada (ajustes individuales).
    public bool IsAdvancedModuleView { get; set; } = false;

    public bool WarnBeforeNonReversible { get; set; } = true;
    public bool CreateRestorePointBeforeRun { get; set; } = false;
    public bool NotifyOnFinish { get; set; } = true;
    public bool AutoUpdateEnabled { get; set; } = false;
    public bool MinimizeToTray { get; set; } = false;
    public bool ShowGamingDisclaimer { get; set; } = true;
    public bool TermsAccepted { get; set; } = false;

    // Rate limiting: activación de licencia
    public int LicenseFailedAttempts { get; set; } = 0;
    public string? LicenseLockedUntil { get; set; } = null;
}
