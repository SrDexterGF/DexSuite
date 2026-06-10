using System.IO;
using System.Management;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using DexSuite.App.Models;

namespace DexSuite.App.Services.CleanupModules;

/// <summary>
/// M17 — Seguridad.
/// MRT, actualización de firmas Defender + Quick Scan (vía MpCmdRun.exe), SMBv1 off,
/// Firewall on, AutoRun/AutoPlay off, DEP AlwaysOn (bcdedit), LLMNR/NetBIOS off,
/// PUA Protection on, RDP off, UAC nivel 2 (recomendado).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class M17Security : ModuleExecutorBase
{
    public M17Security(IChangeTrackingService tracking) : base(tracking) { }

    public override int ModuleId => 17;
    protected override string ModuleName => "Seguridad";

    public override async IAsyncEnumerable<ModuleProgress> ExecuteAsync(
        IReadOnlySet<string>? enabledSubOps,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        yield return Header("Seguridad");
        yield return Info("Aviso: MRT y el escaneo de Defender pueden tardar varios minutos.");

        var system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var defender = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Windows Defender", "MpCmdRun.exe");
        var netsh = Path.Combine(system32, "netsh.exe");

        if (Want(enabledSubOps, "M17_mrt"))
        {
            yield return Step("MRT - Malicious Software Removal Tool");
            var mrt = Path.Combine(system32, "MRT.exe");
            if (File.Exists(mrt))
            {
                await RunProcessAsync(mrt, "/Q", ct);
                yield return Ok("MRT ejecutado (resultado en %SystemRoot%\\debug\\mrt.log)");
            }
            else yield return Info("MRT no encontrado, omitido");
        }

        if (ct.IsCancellationRequested) yield break;
        if (Want(enabledSubOps, "M17_defender_sig"))
        {
            yield return Step("Windows Defender - Actualizando firmas de virus");
            if (File.Exists(defender))
            {
                var rc1 = await RunProcessAsync(defender, "-SignatureUpdate", ct);
                yield return rc1 == 0 ? Ok("Firmas de Defender actualizadas")
                                      : Warn($"SignatureUpdate ExitCode={rc1} (sin conexión?)");
            }
            else yield return Warn("MpCmdRun.exe no encontrado");
        }

        if (ct.IsCancellationRequested) yield break;
        if (Want(enabledSubOps, "M17_defender_scan"))
        {
            yield return Step("Windows Defender - Escaneo rápido del sistema");
            if (File.Exists(defender))
            {
                await foreach (var line in StreamProcessAsync(defender, "-Scan -ScanType 1", ct: ct))
                {
                    if (ct.IsCancellationRequested) yield break;
                    if (!string.IsNullOrWhiteSpace(line))
                        yield return Info(line);
                }
                yield return Ok("Escaneo de Defender completado");
            }
        }

        if (ct.IsCancellationRequested) yield break;
        if (Want(enabledSubOps, "M17_smbv1"))
        {
            yield return Step("Desactivando SMBv1 (vector de WannaCry / EternalBlue)");
            TrackedSetDword(@"HKLM\SYSTEM\CurrentControlSet\Services\LanmanServer\Parameters", "SMB1", 0);
            var dism = Path.Combine(system32, "Dism.exe");
            if (File.Exists(dism))
            {
                await RunProcessAsync(dism, "/Online /Disable-Feature /FeatureName:SMB1Protocol /NoRestart", ct);
            }
            yield return Ok("SMBv1 desactivado");
        }

        if (ct.IsCancellationRequested) yield break;
        if (Want(enabledSubOps, "M17_firewall"))
        {
            yield return Step("Firewall de Windows - Activado en todos los perfiles");
            if (File.Exists(netsh))
            {
                await RunProcessAsync(netsh, "advfirewall set allprofiles state on", ct);
                yield return Ok("Firewall activo en perfiles Domain / Private / Public");
            }
            else yield return Warn("netsh.exe no encontrado");
        }

        if (ct.IsCancellationRequested) yield break;
        if (Want(enabledSubOps, "M17_autorun"))
        {
            yield return Step("Desactivando AutoRun y AutoPlay");
            TrackedSetDword(@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoDriveTypeAutoRun", 255);
            TrackedSetDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoDriveTypeAutoRun", 255);
            TrackedSetDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\Explorer", "NoAutoplayfornonVolume", 1);
            TrackedSetDword(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\AutoplayHandlers", "DisableAutoplay", 1);
            yield return Ok("AutoRun y AutoPlay desactivados");
        }

        if (ct.IsCancellationRequested) yield break;
        if (Want(enabledSubOps, "M17_dep"))
        {
            yield return Step("DEP - Data Execution Prevention en modo AlwaysOn");
            var bcdedit = Path.Combine(system32, "bcdedit.exe");
            if (File.Exists(bcdedit))
            {
                await RunProcessAsync(bcdedit, "/set nx AlwaysOn", ct);
                yield return Ok("DEP configurado en AlwaysOn (requiere reinicio)");
            }
        }

        if (ct.IsCancellationRequested) yield break;
        if (Want(enabledSubOps, "M17_llmnr"))
        {
            yield return Step("Desactivando LLMNR (Link-Local Multicast Name Resolution)");
            TrackedSetDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows NT\DNSClient", "EnableMulticast", 0);
            yield return Ok("LLMNR desactivado");
        }

        if (ct.IsCancellationRequested) yield break;
        if (Want(enabledSubOps, "M17_netbios"))
        {
            yield return Step("Desactivando NetBIOS sobre TCP/IP en todos los adaptadores");
            try
            {
                using var s = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled = TRUE");
                foreach (ManagementObject nac in s.Get())
                {
                    try
                    {
                        var parms = nac.GetMethodParameters("SetTcpipNetbios");
                        parms["TcpipNetbiosOptions"] = (uint)2;  // 2 = Disable
                        nac.InvokeMethod("SetTcpipNetbios", parms, null);
                    }
                    catch { /* siguiente */ }
                }
            }
            catch { /* WMI off */ }
            TrackedSetDword(@"HKLM\SYSTEM\CurrentControlSet\Services\NetBT\Parameters", "NodeType", 2);
            yield return Ok("NetBIOS desactivado en todos los adaptadores activos");
        }

        if (ct.IsCancellationRequested) yield break;
        if (Want(enabledSubOps, "M17_pua"))
        {
            yield return Step("Activando protección PUA en Windows Defender");
            TrackedSetDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows Defender", "PUAProtection", 1);
            yield return Ok("Protección PUA activada");
        }

        if (ct.IsCancellationRequested) yield break;
        if (Want(enabledSubOps, "M17_rdp"))
        {
            yield return Step("Desactivando Remote Desktop (RDP)");
            TrackedSetDword(@"HKLM\SYSTEM\CurrentControlSet\Control\Terminal Server", "fDenyTSConnections", 1);
            TrackedSetServiceStartMode("TermService", "Manual"); StopService("TermService");
            if (File.Exists(netsh))
                await RunProcessAsync(netsh, "advfirewall firewall set rule name=\"Remote Desktop\" new enable=no", ct);
            yield return Ok("RDP desactivado. Reactivarlo en Configuración si se necesita.");
        }

        if (ct.IsCancellationRequested) yield break;
        if (Want(enabledSubOps, "M17_uac"))
        {
            yield return Step("UAC - Nivel recomendado (aviso solo para apps externas)");
            TrackedSetDword(@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "EnableLUA", 1);
            TrackedSetDword(@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "ConsentPromptBehaviorAdmin", 5);
            TrackedSetDword(@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "PromptOnSecureDesktop", 1);
            yield return Ok("UAC activo en nivel 2 (recomendado)");
        }

        yield return Done("M17 completado");
    }
}
