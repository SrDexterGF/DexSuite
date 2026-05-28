namespace DexSuite.App.Services;

/// <summary>Resultado de una operación de limpieza rápida.</summary>
public record QuickCleanResult(long BytesFreed, int FilesDeleted);

/// <summary>Limpieza rápida de archivos temporales y papelera de reciclaje.</summary>
public interface IQuickCleanService
{
    Task<QuickCleanResult> CleanAsync(CancellationToken ct = default);
}
