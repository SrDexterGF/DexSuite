using DexSuite.App.Models;

namespace DexSuite.App.Services;

public interface ISystemInfoService
{
    Task<SystemInfo> GetSystemInfoAsync();
}
