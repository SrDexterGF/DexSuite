namespace DexSuite.App.Services;

/// <summary>
/// Localiza y elimina los artefactos que DexSuite y Velopack dejan en
/// %LocalAppData% (carpetas de staging DexSuite_*).
/// No toca la carpeta DexSuiteKeyGen ni el directorio de datos de la app.
/// </summary>
public interface IAppSelfCleanupService
{
    /// <summary>
    /// Elimina las carpetas de staging de Velopack encontradas.
    /// </summary>
    /// <returns>(foldersDeleted, bytesFreed)</returns>
    Task<(int Folders, long Bytes)> CleanAsync(CancellationToken ct = default);
}
