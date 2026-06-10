using System.IO;
using System.Management;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using DexSuite.App.Models;

namespace DexSuite.App.Services.CleanupModules;

/// <summary>
/// M18 — SSD (TRIM y salud SMART).
/// fsutil DisableDeleteNotify=0 para NTFS y ReFS, detecta SSDs vía WMI
/// (MSFT_PhysicalDisk + MSFT_StorageReliabilityCounter en root\Microsoft\Windows\Storage),
/// lanza TRIM con defrag.exe X: /L /O por cada SSD y reporta temperatura y desgaste.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class M18SsdTrim : ModuleExecutorBase
{
    public override int ModuleId => 18;

    public override async IAsyncEnumerable<ModuleProgress> ExecuteAsync(
        IReadOnlySet<string>? enabledSubOps,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        yield return Header("SSD - TRIM y salud SMART");

        var system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var fsutil   = Path.Combine(system32, "fsutil.exe");
        var defrag   = Path.Combine(system32, "defrag.exe");

        if (Want(enabledSubOps, "M18_trim_enable"))
        {
            yield return Step("Asegurando que TRIM está activado a nivel sistema");
            if (File.Exists(fsutil))
            {
                await RunProcessAsync(fsutil, "behavior set DisableDeleteNotify NTFS 0", ct);
                await RunProcessAsync(fsutil, "behavior set DisableDeleteNotify ReFS 0", ct);
                yield return Ok("TRIM activado para NTFS y ReFS");
            }
            else yield return Warn("fsutil.exe no encontrado");
        }

        if (!Want(enabledSubOps, "M18_trim_smart"))
        {
            yield return Done("M18 completado");
            yield break;
        }

        yield return Info("El TRIM puede tardar entre 10 y 60 segundos por SSD.");

        if (ct.IsCancellationRequested) yield break;
        yield return Step("Detectando SSDs, aplicando TRIM y leyendo salud SMART");

        var ssds = new List<(int Number, string Name, string Health, IList<char> DriveLetters)>();
        string? scanErr = null;
        try
        {
            using var diskSearcher = new ManagementObjectSearcher(
                @"\\.\ROOT\Microsoft\Windows\Storage",
                "SELECT DeviceId, FriendlyName, MediaType, HealthStatus FROM MSFT_PhysicalDisk");
            foreach (ManagementObject pd in diskSearcher.Get())
            {
                // MediaType: 4 = SSD, 3 = HDD, 5 = SCM (Storage Class Memory).
                var mediaType = Convert.ToInt32(pd["MediaType"] ?? 0);
                if (mediaType != 4) continue;

                var deviceId = Convert.ToInt32(pd["DeviceId"] ?? -1);
                var friendly = pd["FriendlyName"]?.ToString() ?? "(SSD)";
                var health   = HealthName(Convert.ToInt32(pd["HealthStatus"] ?? 0));

                // Letras de unidad por DiskNumber vía CIM Win32_DiskPartition→Win32_LogicalDiskToPartition.
                var letters = GetDriveLettersForDisk(deviceId);
                ssds.Add((deviceId, friendly, health, letters));
            }
        }
        catch (Exception ex) { scanErr = ex.Message; }
        if (scanErr is not null)
            yield return Warn($"No se pudieron leer SSDs vía Storage WMI: {scanErr}");

        if (ssds.Count == 0)
        {
            yield return Info("No hay SSDs en este equipo");
            yield return Done("M18 completado");
            yield break;
        }

        yield return Info($"{ssds.Count} SSD(s) detectado(s)");

        foreach (var (num, name, health, letters) in ssds)
        {
            if (ct.IsCancellationRequested) yield break;
            yield return Info($"{name}  [{health}]");
            foreach (var letter in letters)
            {
                if (ct.IsCancellationRequested) yield break;
                yield return Info($"  TRIM en {letter}:");
                if (File.Exists(defrag))
                    await RunProcessAsync(defrag, $"{letter}: /L /O", ct);
            }

            // SMART via MSFT_StorageReliabilityCounter para este disk.
            var smartLines = new List<ModuleProgress>();
            bool smartErr = false;
            try
            {
                using var src = new ManagementObjectSearcher(
                    @"\\.\ROOT\Microsoft\Windows\Storage",
                    $"SELECT Temperature, Wear FROM MSFT_StorageReliabilityCounter WHERE DeviceId='{num}'");
                foreach (ManagementObject r in src.Get())
                {
                    var temp = r["Temperature"];
                    var wear = r["Wear"];
                    if (temp is not null) smartLines.Add(Info($"  Temperatura : {temp} °C"));
                    if (wear is not null)
                    {
                        smartLines.Add(Info($"  Desgaste    : {wear} /100"));
                        if (Convert.ToInt32(wear) > 80)
                            smartLines.Add(Warn("  Wear alto - plantea reemplazo."));
                    }
                }
            }
            catch { smartErr = true; }

            if (smartErr || smartLines.Count == 0)
                yield return Info("  (sin datos SMART disponibles)");
            else
                foreach (var l in smartLines) yield return l;
        }
        yield return Ok("TRIM y salud SMART completados");

        yield return Done("M18 completado");
    }

    private static string HealthName(int status) => status switch
    {
        0 => "Healthy",
        1 => "Warning",
        2 => "Unhealthy",
        5 => "Unknown",
        _ => $"Status {status}",
    };

    /// <summary>
    /// Devuelve las letras de unidad asociadas a un DiskNumber físico.
    /// </summary>
    private static IList<char> GetDriveLettersForDisk(int diskNumber)
    {
        var letters = new List<char>();
        try
        {
            // Win32_DiskDriveToDiskPartition: cada DiskDrive con DeviceID '\\\\.\\PHYSICALDRIVE{n}'
            // Vamos por el camino corto: usamos Win32_DiskPartition.DeviceID que contiene 'Disk #{n}'.
            using var parts = new ManagementObjectSearcher(
                $"SELECT * FROM Win32_DiskPartition WHERE DiskIndex = {diskNumber}");
            foreach (ManagementObject part in parts.Get())
            {
                using var lds = new ManagementObjectSearcher(
                    $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{part["DeviceID"]}'}} " +
                    "WHERE AssocClass = Win32_LogicalDiskToPartition");
                foreach (ManagementObject ld in lds.Get())
                {
                    var deviceId = ld["DeviceID"]?.ToString();
                    if (!string.IsNullOrEmpty(deviceId) && deviceId.Length >= 1)
                        letters.Add(deviceId[0]);
                }
            }
        }
        catch { /* ignora */ }
        return letters;
    }
}
