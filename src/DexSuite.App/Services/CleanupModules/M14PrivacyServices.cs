using System.IO;
using System.Management;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using DexSuite.App.Models;

namespace DexSuite.App.Services.CleanupModules;

/// <summary>
/// M14 — Privacidad y servicios.
/// Desactiva apps sugeridas/silenciosas, ubicación, Widgets (AppX WebExperienceHost),
/// WPBT, servicios remotos innecesarios, ajusta SvcHostSplitThresholdInKB a la RAM
/// instalada, desactiva telemetría de PS7 y atajos de accesibilidad molestos.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class M14PrivacyServices : ModuleExecutorBase
{
    public M14PrivacyServices(IChangeTrackingService tracking) : base(tracking) { }

    public override int ModuleId => 14;
    protected override string ModuleName => "Privacidad y Servicios";

    public override async IAsyncEnumerable<ModuleProgress> ExecuteAsync(
        IReadOnlySet<string>? enabledSubOps,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        yield return Header("Privacidad y Servicios");

        if (Want(enabledSubOps, "M14_silent_apps"))
        {
            yield return Step("Desactivando instalación silenciosa de apps sugeridas");
            TrackedSetDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableWindowsConsumerFeatures", 1);
            TrackedSetDword(@"HKCU\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SilentInstalledAppsEnabled", 0);
            TrackedSetDword(@"HKCU\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SystemPaneSuggestionsEnabled", 0);
            TrackedSetDword(@"HKCU\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338393Enabled", 0);
            TrackedSetDword(@"HKCU\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-353694Enabled", 0);
            TrackedSetDword(@"HKCU\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-353696Enabled", 0);
            yield return Ok("Instalación silenciosa de apps desactivada");
        }

        if (ct.IsCancellationRequested) yield break;
        if (Want(enabledSubOps, "M14_location"))
        {
            yield return Step("Desactivando rastreo de ubicación");
            TrackedSetString(@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location", "Value", "Deny");
            TrackedSetDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors", "DisableLocation", 1);
            TrackedSetDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors", "DisableLocationScripting", 1);
            yield return Ok("Ubicación desactivada");
        }

        if (ct.IsCancellationRequested) yield break;
        if (Want(enabledSubOps, "M14_widgets"))
        {
            yield return Step("Quitando los Widgets de la barra de tareas");
            TrackedSetDword(@"HKLM\SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests", 0);
            TrackedSetDword(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarDa", 0);
            // Guard runtime: la API WinRT existe en Win10 1507+ (10.0.10240). El target
            // del proyecto (net8.0-windows10.0.19041) garantiza que estamos por encima.
            int removed = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10240)
                ? RemoveAppxPackagesByFamily("WebExperienceHost")
                : 0;
            yield return removed > 0
                ? Ok($"Widgets eliminados ({removed} paquete(s) AppX)")
                : Ok("Widgets desactivados por registro (no había AppX que retirar)");
        }

        if (ct.IsCancellationRequested) yield break;
        if (Want(enabledSubOps, "M14_store_search"))
        {
            yield return Step("Quitando resultados de la Store en el buscador");
            TrackedSetDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\Explorer", "NoUseStoreOpenWith", 1);
            TrackedSetDword(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_TrackProgs", 0);
            yield return Ok("Resultados de Store en búsqueda desactivados");
        }

        if (ct.IsCancellationRequested) yield break;
        if (Want(enabledSubOps, "M14_wpbt"))
        {
            yield return Step("Desactivando WPBT (reinstalación de bloatware por BIOS)");
            TrackedSetDword(@"HKLM\SYSTEM\CurrentControlSet\Control\Session Manager", "DisableWpbtExecution", 1);
            yield return Ok("WPBT desactivado");
        }

        if (ct.IsCancellationRequested) yield break;
        if (Want(enabledSubOps, "M14_services"))
        {
            yield return Step("Desactivando servicios innecesarios");
            TrackedSetServiceStartMode("MapsBroker", "Manual");     StopService("MapsBroker");
            TrackedSetServiceStartMode("RemoteRegistry", "Disabled"); StopService("RemoteRegistry");
            TrackedSetServiceStartMode("RemoteAccess",   "Disabled"); StopService("RemoteAccess");
            TrackedSetServiceStartMode("SharedAccess",   "Disabled"); StopService("SharedAccess");
            TrackedSetServiceStartMode("CscService",     "Disabled"); StopService("CscService");
            TrackedSetServiceStartMode("ssh-agent",      "Disabled"); StopService("ssh-agent");
            yield return Ok("Servicios innecesarios desactivados");
        }

        if (ct.IsCancellationRequested) yield break;
        if (Want(enabledSubOps, "M14_svchost"))
        {
            yield return Step("Ajustando SvcHost según la RAM instalada");
            long totalRamBytes = 0;
            string? ramErr = null;
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Capacity FROM Win32_PhysicalMemory");
                foreach (ManagementObject m in searcher.Get())
                    totalRamBytes += Convert.ToInt64(m["Capacity"] ?? 0);
            }
            catch (Exception ex) { ramErr = ex.Message; }
            if (ramErr is null)
            {
                var ramInKB = (int)(totalRamBytes / 1024);
                TrackedSetDword(@"HKLM\SYSTEM\CurrentControlSet\Control", "SvcHostSplitThresholdInKB", ramInKB);
                yield return Ok($"SvcHost ajustado a {ramInKB / 1024 / 1024} GB de RAM (umbral = {ramInKB} KB)");
            }
            else yield return Warn($"No se pudo leer la RAM: {ramErr}");
        }

        if (ct.IsCancellationRequested) yield break;
        if (Want(enabledSubOps, "M14_ps7_telemetry"))
        {
            yield return Step("Desactivando telemetría de PowerShell 7 (si está instalado)");
            var ps7 = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "PowerShell", "7", "pwsh.exe");
            if (File.Exists(ps7))
            {
                string? envErr = null;
                try
                {
                    Environment.SetEnvironmentVariable("POWERSHELL_TELEMETRY_OPTOUT", "1",
                        EnvironmentVariableTarget.Machine);
                }
                catch (Exception ex) { envErr = ex.Message; }
                yield return envErr is null
                    ? Ok("Telemetría de PS7 desactivada")
                    : Warn($"No se pudo escribir env var: {envErr}");
            }
            else yield return Info("PowerShell 7 no instalado, omitido");
        }

        if (ct.IsCancellationRequested) yield break;
        if (Want(enabledSubOps, "M14_sticky_keys"))
        {
            yield return Step("Desactivando Sticky Keys y atajos de accesibilidad molestos");
            TrackedSetString(@"HKCU\Control Panel\Accessibility\StickyKeys",        "Flags", "506");
            TrackedSetString(@"HKCU\Control Panel\Accessibility\ToggleKeys",        "Flags", "58");
            TrackedSetString(@"HKCU\Control Panel\Accessibility\Keyboard Response", "Flags", "122");
            yield return Ok("Sticky Keys desactivadas");
        }

        yield return Done("M14 completado");
        await Task.CompletedTask;
    }

    /// <summary>
    /// Elimina paquetes AppX cuyo FamilyName contenga el patrón indicado.
    /// Usa Windows.Management.Deployment.PackageManager (WinRT) — sin PowerShell.
    /// </summary>
    [SupportedOSPlatform("windows10.0.10240.0")]
    private static int RemoveAppxPackagesByFamily(string substringPattern)
    {
        int removed = 0;
        try
        {
            var pm = new global::Windows.Management.Deployment.PackageManager();
            // FindPackages(currentUser) requiere namespace y elevación; con admin sirve.
            var packages = pm.FindPackages().ToList();
            foreach (var pkg in packages)
            {
                var id = pkg.Id;
                var familyName = id?.FamilyName ?? string.Empty;
                if (id is null) continue;
                if (familyName.Contains(substringPattern, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var op = pm.RemovePackageAsync(id.FullName);
                        op.AsTask().Wait();
                        removed++;
                    }
                    catch { /* paquete protegido / en uso */ }
                }
            }
        }
        catch { /* PackageManager no disponible o sin permisos */ }
        return removed;
    }
}
