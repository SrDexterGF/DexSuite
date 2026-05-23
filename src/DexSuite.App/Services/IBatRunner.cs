namespace DexSuite.App.Services;

public interface IBatRunner
{
    /// <summary>
    /// Lanza el .bat de DexSuite con los modulos indicados y devuelve cada linea
    /// de stdout / stderr a medida que aparecen.
    /// </summary>
    IAsyncEnumerable<string> RunAsync(
        string batPath,
        IReadOnlyList<int> selectedModuleIds,
        CancellationToken ct = default);
}
