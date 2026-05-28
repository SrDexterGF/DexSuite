using DexSuite.App.Models;

namespace DexSuite.App.Services;

public interface IModuleCatalog
{
    IReadOnlyList<CleanupModule> GetAll();
}
