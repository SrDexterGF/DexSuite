using System.IO;
using System.Management;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using DexSuite.App.Models;

namespace DexSuite.App.Services.CleanupModules;

/// <summary>
/// M15 — Rendimiento y latencia.
/// Power Throttling off, MMCSS para juegos, Game Mode, FSE off, timeouts mínimos,
/// hibernación off, sincronización de cuenta off, transparencia off, apps en
/// segundo plano off, recopilación de escritura/voz off, HAGS on, servicios de
/// diagnóstico ajustados, BCD timer fijo.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class M15Performance : ModuleExecutorBase
{
    public override int ModuleId => 15;

    public override async IAsyncEnumerable<ModuleProgress> ExecuteAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        yield return Header("Rendimiento y Latencia");

        yield return Step("Desactivando Power Throttling");
        SetRegistryDword(@"HKLM\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling", "PowerThrottlingOff", 1);
        yield return Ok("Power Throttling desactivado");

        if (ct.IsCancellationRequested) yield break;
        yield return Step("MMCSS - Prioridad máxima para juegos y multimedia");
        const string sysProfile = @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
        const string gamesTask  = sysProfile + @"\Tasks\Games";
        SetRegistryDword(sysProfile, "NetworkThrottlingIndex", 10);
        SetRegistryDword(sysProfile, "SystemResponsiveness", 0);
        SetRegistryDword(gamesTask, "Affinity", 0);
        SetRegistryString(gamesTask, "Background Only", "False");
        SetRegistryDword(gamesTask, "Clock Rate", 10000);
        SetRegistryDword(gamesTask, "GPU Priority", 8);
        SetRegistryDword(gamesTask, "Priority", 6);
        SetRegistryString(gamesTask, "Scheduling Category", "High");
        SetRegistryString(gamesTask, "SFIO Priority", "High");
        yield return Ok("MMCSS configurado para máximo rendimiento en juegos");

        if (ct.IsCancellationRequested) yield break;
        yield return Step("Activando Game Mode");
        SetRegistryDword(@"HKCU\SOFTWARE\Microsoft\GameBar", "AllowAutoGameMode", 1);
        SetRegistryDword(@"HKCU\SOFTWARE\Microsoft\GameBar", "AutoGameModeEnabled", 1);
        yield return Ok("Game Mode activado");

        if (ct.IsCancellationRequested) yield break;
        yield return Step("Desactivando Full Screen Optimizations");
        SetRegistryDword(@"HKCU\System\GameConfigStore", "GameDVR_FSEBehaviorMode", 2);
        SetRegistryDword(@"HKCU\System\GameConfigStore", "GameDVR_HonorUserFSEBehaviorMode", 0);
        SetRegistryDword(@"HKCU\System\GameConfigStore", "GameDVR_DXGIHonorFSEWindowsCompatible", 1);
        SetRegistryDword(@"HKCU\System\GameConfigStore", "GameDVR_EFSEFeatureFlags", 0);
        yield return Ok("Full Screen Optimizations desactivadas");

        if (ct.IsCancellationRequested) yield break;
        yield return Step("Reduciendo tiempos de espera del sistema");
        SetRegistryString(@"HKCU\Control Panel\Desktop", "AutoEndTasks", "1");
        SetRegistryString(@"HKCU\Control Panel\Desktop", "HungAppTimeout", "1000");
        SetRegistryString(@"HKCU\Control Panel\Desktop", "WaitToKillAppTimeout", "2000");
        SetRegistryString(@"HKCU\Control Panel\Desktop", "LowLevelHooksTimeout", "1000");
        SetRegistryString(@"HKCU\Control Panel\Desktop", "MenuShowDelay", "0");
        SetRegistryString(@"HKLM\SYSTEM\CurrentControlSet\Control", "WaitToKillServiceTimeout", "2000");
        yield return Ok("Tiempos de espera reducidos");

        if (ct.IsCancellationRequested) yield break;
        yield return Step("Mantenimiento automático (activo; necesario para TRIM del SSD)");
        SetRegistryDword(@"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Schedule\Maintenance",
            "MaintenanceDisabled", 0);
        yield return Ok("Mantenimiento automático preservado");

        if (ct.IsCancellationRequested) yield break;
        yield return Step("Desactivando hibernación y liberando hiberfil.sys");
        var powercfg = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System), "powercfg.exe");
        if (File.Exists(powercfg))
            await RunProcessAsync(powercfg, "/hibernate off", ct);
        SetRegistryDword(@"HKLM\SYSTEM\CurrentControlSet\Control\Power", "HibernateEnabled", 0);
        yield return Ok("Hibernación desactivada");

        if (ct.IsCancellationRequested) yield break;
        yield return Step("Desactivando sincronización de configuración con la cuenta Microsoft");
        const string syncRoot = @"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\SettingSync";
        SetRegistryDword(syncRoot, "SyncPolicy", 5);
        foreach (var grp in new[] { "Personalization", "BrowserSettings", "Credentials", "Accessibility", "Windows" })
            SetRegistryDword($@"{syncRoot}\Groups\{grp}", "Enabled", 0);
        yield return Ok("Sincronización de cuenta desactivada");

        if (ct.IsCancellationRequested) yield break;
        yield return Step("Desactivando efectos de transparencia");
        SetRegistryDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "EnableTransparency", 0);
        yield return Ok("Transparencia desactivada");

        if (ct.IsCancellationRequested) yield break;
        yield return Step("Desactivando apps en segundo plano");
        SetRegistryDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications",
            "GlobalUserDisabled", 1);
        SetRegistryDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Search",
            "BackgroundAppGlobalToggle", 0);
        yield return Ok("Apps en segundo plano desactivadas");

        if (ct.IsCancellationRequested) yield break;
        yield return Step("Desactivando recopilación de datos de escritura y voz");
        SetRegistryDword(@"HKCU\SOFTWARE\Microsoft\Personalization\Settings", "AcceptedPrivacyPolicy", 0);
        SetRegistryDword(@"HKCU\SOFTWARE\Microsoft\InputPersonalization", "RestrictImplicitInkCollection", 1);
        SetRegistryDword(@"HKCU\SOFTWARE\Microsoft\InputPersonalization", "RestrictImplicitTextCollection", 1);
        SetRegistryDword(@"HKCU\SOFTWARE\Microsoft\InputPersonalization\TrainedDataStore", "HarvestContacts", 0);
        SetRegistryDword(@"HKCU\SOFTWARE\Microsoft\Speech_OneCore\Settings\OnlineSpeechPrivacy", "HasAccepted", 0);
        yield return Ok("Recopilación de escritura y voz desactivada");

        if (ct.IsCancellationRequested) yield break;
        yield return Step("HAGS - Hardware Accelerated GPU Scheduling");
        var gpus = new List<string>();
        string? gpuErr = null;
        try
        {
            using var s = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
            foreach (ManagementObject g in s.Get())
            {
                var name = g["Name"]?.ToString();
                if (!string.IsNullOrWhiteSpace(name))
                    gpus.Add(name);
            }
        }
        catch (Exception ex) { gpuErr = ex.Message; }
        foreach (var gpu in gpus) yield return Info($"GPU detectada: {gpu}");
        if (gpuErr is not null) yield return Warn($"No se pudo leer GPU: {gpuErr}");
        SetRegistryDword(@"HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 2);
        yield return Ok("HAGS activado (requiere reinicio; solo GPU NVIDIA 10xx+/AMD RX5000+)");

        if (ct.IsCancellationRequested) yield break;
        yield return Step("Desactivando servicios de diagnóstico innecesarios");
        SetServiceStartMode("diagsvc", "Disabled");        StopService("diagsvc");
        SetServiceStartMode("DPS", "Manual");              StopService("DPS");
        SetServiceStartMode("WdiServiceHost", "Manual");   StopService("WdiServiceHost");
        SetServiceStartMode("WdiSystemHost", "Manual");    StopService("WdiSystemHost");
        SetServiceStartMode("FontCache", "Automatic");
        SetServiceStartMode("GraphicsPerfSvc", "Disabled"); StopService("GraphicsPerfSvc");
        SetServiceStartMode("stisvc", "Manual");           StopService("stisvc");
        SetServiceStartMode("PcaSvc", "Disabled");         StopService("PcaSvc");
        SetServiceStartMode("Wecsvc", "Disabled");         StopService("Wecsvc");
        yield return Ok("Servicios de diagnóstico configurados");

        if (ct.IsCancellationRequested) yield break;
        yield return Step("BCD: timer de hardware fijo para menos jitter");
        var bcdedit = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System), "bcdedit.exe");
        if (File.Exists(bcdedit))
        {
            await RunProcessAsync(bcdedit, "/set disabledynamictick yes", ct);
            await RunProcessAsync(bcdedit, "/set useplatformtick yes", ct);
            await RunProcessAsync(bcdedit, "/deletevalue useplatformclock", ct);
            yield return Ok("BCD: Dynamic Tick desactivado, Platform Tick activado");
        }
        else yield return Warn("bcdedit.exe no encontrado");

        yield return Done("M15 completado");
    }
}
