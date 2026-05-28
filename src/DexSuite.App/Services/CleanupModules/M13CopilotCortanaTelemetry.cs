using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using DexSuite.App.Models;

namespace DexSuite.App.Services.CleanupModules;

/// <summary>
/// M13 — Copilot, Cortana y Telemetría.
/// Deshabilita Windows Copilot, Cortana, telemetría (nivel 0), DiagTrack,
/// Timeline, AdvertisingID y CEIP — todo vía registro y WMI (servicios).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class M13CopilotCortanaTelemetry : ModuleExecutorBase
{
    public M13CopilotCortanaTelemetry(IChangeTrackingService tracking) : base(tracking) { }

    public override int ModuleId => 13;
    protected override string ModuleName => "Copilot, Cortana y Telemetría";

    public override async IAsyncEnumerable<ModuleProgress> ExecuteAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        yield return Header("Copilot, Cortana y Telemetría");

        yield return Step("Desactivando Windows Copilot");
        TrackedSetDword(@"HKCU\Software\Policies\Microsoft\Windows\WindowsCopilot",       "TurnOffWindowsCopilot", 1);
        TrackedSetDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot",       "TurnOffWindowsCopilot", 1);
        TrackedSetDword(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowCopilotButton", 0);
        TrackedSetDword(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Search",            "BingSearchEnabled", 0);
        yield return Ok("Copilot desactivado");

        if (ct.IsCancellationRequested) yield break;
        yield return Step("Desactivando Cortana");
        TrackedSetDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowCortana", 0);
        TrackedSetDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowCortanaAboveLock", 0);
        TrackedSetDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowSearchToUseLocation", 0);
        TrackedSetDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Search", "DisableWebSearch", 1);
        TrackedSetDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Search", "ConnectedSearchUseWeb", 0);
        KillProcess("Cortana.exe");
        yield return Ok("Cortana desactivada");

        if (ct.IsCancellationRequested) yield break;
        yield return Step("Telemetría a nivel 0 (mínimo posible)");
        TrackedSetDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\DataCollection",                "AllowTelemetry", 0);
        TrackedSetDword(@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection", "AllowTelemetry", 0);
        TrackedSetDword(@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection", "MaxTelemetryAllowed", 0);
        yield return Ok("Telemetría configurada al nivel 0");

        if (ct.IsCancellationRequested) yield break;
        yield return Step("Desactivando los servicios de telemetría");
        TrackedSetServiceStartMode("DiagTrack", "Disabled"); StopService("DiagTrack");
        TrackedSetServiceStartMode("dmwappushservice", "Disabled"); StopService("dmwappushservice");
        TrackedSetServiceStartMode("diagnosticshub.standardcollector.service", "Disabled");
        TrackedSetServiceStartMode("WerSvc", "Disabled"); StopService("WerSvc");
        yield return Ok("Servicios de telemetría desactivados");

        if (ct.IsCancellationRequested) yield break;
        yield return Step("Desactivando Timeline, AdvertisingID y CEIP");
        TrackedSetDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\AppCompat", "AITEnable", 0);
        TrackedSetDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\AppCompat", "DisableInventory", 1);
        TrackedSetDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\AppCompat", "DisablePCA", 1);
        TrackedSetDword(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Privacy",
            "TailoredExperiencesWithDiagnosticDataEnabled", 0);
        TrackedSetDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivities", 0);
        TrackedSetDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\System", "UploadUserActivities", 0);
        TrackedSetDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\System", "EnableActivityFeed", 0);
        TrackedSetDword(@"HKCU\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0);
        TrackedSetDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo", "DisabledByGroupPolicy", 1);
        TrackedSetDword(@"HKLM\SOFTWARE\Policies\Microsoft\SQMClient\Windows", "CEIPEnable", 0);
        TrackedSetDword(@"HKLM\SOFTWARE\Microsoft\SQMClient\Windows", "CEIPEnable", 0);
        TrackedSetDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Error Reporting", "Disabled", 1);
        TrackedSetDword(@"HKLM\SOFTWARE\Microsoft\Windows\Windows Error Reporting", "Disabled", 1);
        yield return Ok("Timeline, AdvertisingID y CEIP desactivados");

        yield return Done("M13 completado");
        await Task.CompletedTask;
    }
}
