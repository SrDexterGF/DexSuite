namespace DexSuite.App.Models;

/// <summary>Información de un disco físico/lógico.</summary>
public record DiskInfo(
    string Letter,
    string Label,
    long TotalGb,
    long FreeGb,
    string FileSystem);

/// <summary>Snapshot de las especificaciones de hardware del sistema.</summary>
public record SystemInfo(
    string CpuName,
    int    CpuCores,
    int    CpuLogical,
    int    CpuSpeedMhz,
    int    RamTotalGb,
    int    RamAvailableGb,
    string GpuName,
    string OsName,
    string OsBuild,
    IReadOnlyList<DiskInfo> Disks);
