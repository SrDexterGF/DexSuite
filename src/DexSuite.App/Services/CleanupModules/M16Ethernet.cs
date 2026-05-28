using System.IO;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using DexSuite.App.Models;
using Microsoft.Win32;

namespace DexSuite.App.Services.CleanupModules;

/// <summary>
/// M16 — Ethernet (optimización de red).
/// Configura adaptadores Ethernet (advanced properties vía registro Class),
/// TCP global vía netsh, parámetros del stack IP vía registro, QoS = 0%,
/// DNS Google + Cloudflare vía WMI Win32_NetworkAdapterConfiguration y vacía
/// caches ARP/NetBIOS/DNS.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class M16Ethernet : ModuleExecutorBase
{
    public override int ModuleId => 16;

    // Class GUID de adaptadores de red (NDIS NetClass).
    private const string NetClassPath =
        @"HKLM\SYSTEM\CurrentControlSet\Control\Class\{4D36E972-E325-11CE-BFC1-08002BE10318}";

    public override async IAsyncEnumerable<ModuleProgress> ExecuteAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        yield return Header("Ethernet - Optimización Máxima");

        yield return Step("Detectando y configurando adaptadores Ethernet activos");
        var ethernetAdapters = NetworkInterface.GetAllNetworkInterfaces()
            .Where(i => i.NetworkInterfaceType == NetworkInterfaceType.Ethernet
                     && i.OperationalStatus == OperationalStatus.Up)
            .ToList();

        foreach (var nic in ethernetAdapters)
        {
            if (ct.IsCancellationRequested) yield break;
            yield return Info($"Adaptador encontrado: {nic.Name}");
            ApplyAdvancedProperties(nic.Id);
        }
        yield return Ok($"NIC configurada en {ethernetAdapters.Count} adaptador(es)");

        if (ct.IsCancellationRequested) yield break;
        yield return Step("TCP Stack - Autotuning y configuración global");
        var netsh = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System), "netsh.exe");
        if (File.Exists(netsh))
        {
            await RunProcessAsync(netsh, "int tcp set global autotuninglevel=normal", ct);
            await RunProcessAsync(netsh, "int tcp set global chimney=disabled", ct);
            await RunProcessAsync(netsh, "int tcp set global dca=enabled", ct);
            await RunProcessAsync(netsh, "int tcp set global ecncapability=disabled", ct);
            await RunProcessAsync(netsh, "int tcp set global rss=enabled", ct);
            await RunProcessAsync(netsh, "int tcp set global nonsackrttresiliency=disabled", ct);
            yield return Ok("TCP: Autotuning Normal, DCA activado, ECN desactivado");
        }
        else yield return Warn("netsh.exe no encontrado");

        if (ct.IsCancellationRequested) yield break;
        yield return Step("TCP - Nagle off y ACK inmediato en todas las interfaces");
        if (File.Exists(netsh))
        {
            await RunProcessAsync(netsh, "int tcp set global timestamps=disabled", ct);
            await RunProcessAsync(netsh, "int tcp set global initialRto=2000", ct);
        }
        SetRegistryDword(@"HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters", "TcpMaxSynRetransmissions", 2);
        const string tcpInterfaces = @"HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces";
        SetRegistryDword(tcpInterfaces, "TcpAckFrequency", 1);
        SetRegistryDword(tcpInterfaces, "TcpNoDelay", 1);
        foreach (var sub in EnumerateSubKeys(tcpInterfaces))
        {
            SetRegistryDword($@"{tcpInterfaces}\{sub}", "TcpAckFrequency", 1);
            SetRegistryDword($@"{tcpInterfaces}\{sub}", "TcpNoDelay", 1);
        }
        yield return Ok("Nagle desactivado, ACK inmediato en todas las interfaces");

        if (ct.IsCancellationRequested) yield break;
        yield return Step("Parámetros del stack IP");
        const string tcpipParams = @"HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters";
        SetRegistryDword(tcpipParams, "MaxUserPort", 65534);
        SetRegistryDword(tcpipParams, "TcpTimedWaitDelay", 30);
        SetRegistryDword(tcpipParams, "MaxFreeTcbs", 65536);
        SetRegistryDword(tcpipParams, "MaxHashTableSize", 65536);
        SetRegistryDword(tcpipParams, "MaxDupAcksForFastRetransmit", 2);
        yield return Ok("MaxUserPort=65534, TIME_WAIT=30s, MaxFreeTcbs=65536");

        if (ct.IsCancellationRequested) yield break;
        yield return Step("QoS - Eliminando la reserva del 20% de ancho de banda");
        SetRegistryDword(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\Psched", "NonBestEffortLimit", 0);
        SetRegistryDword(@"HKLM\SYSTEM\CurrentControlSet\Services\Psched\Parameters", "NonBestEffortLimit", 0);
        yield return Ok("QoS reserva = 0%");

        if (ct.IsCancellationRequested) yield break;
        yield return Step("Configurando DNS: Google primario + Cloudflare terciario");
        yield return Info("DNS 1: 8.8.8.8 (Google) / DNS 2: 8.8.4.4 (Google) / DNS 3: 1.1.1.1 (Cloudflare)");
        int dnsApplied = SetDnsOnEthernetAdapters(new[] { "8.8.8.8", "8.8.4.4", "1.1.1.1" });
        yield return Ok($"DNS configurado en {dnsApplied} adaptador(es) Ethernet");

        if (ct.IsCancellationRequested) yield break;
        yield return Step("Vaciando cache ARP, NetBIOS y DNS");
        var arp      = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "arp.exe");
        var nbtstat  = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "nbtstat.exe");
        var ipconfig = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "ipconfig.exe");
        if (File.Exists(arp))      await RunProcessAsync(arp, "-d *", ct);
        if (File.Exists(nbtstat))
        {
            await RunProcessAsync(nbtstat, "-R", ct);
            await RunProcessAsync(nbtstat, "-RR", ct);
        }
        if (File.Exists(ipconfig)) await RunProcessAsync(ipconfig, "/flushdns", ct);
        yield return Ok("Cachés de red vaciadas");

        yield return Done("M16 completado");
    }

    /// <summary>
    /// Escribe las advanced properties típicas para gaming en el subkey
    /// de Class correspondiente al GUID del adaptador.
    /// </summary>
    private static void ApplyAdvancedProperties(string adapterGuid)
    {
        // Encontrar el subkey de Class con NetCfgInstanceId == adapterGuid.
        var subKeyPath = FindClassSubKey(adapterGuid);
        if (subKeyPath is null) return;

        // SpeedDuplex: 6 = 1 Gbps Full Duplex. Si el switch no aguanta, lo dejamos en 0 (Auto).
        SetRegistryString(subKeyPath, "*SpeedDuplex", "6");

        // EEE / Green Ethernet / ULP / power saving — todo a 0.
        var disableKeywords = new[]
        {
            "*EEE", "*GreenEthernet", "*ULPMode", "GigabitAdapter", "PowerSavingMode",
            "*WakeOnMagicPacket", "*WakeOnPattern", "WakeOnLink", "*AutoPowerSaveModeEnabled",
            "*FlowControl", "*InterruptModeration",
        };
        foreach (var kw in disableKeywords)
            SetRegistryString(subKeyPath, kw, "0");

        // Buffers grandes.
        SetRegistryString(subKeyPath, "*ReceiveBuffers", "512");
        SetRegistryString(subKeyPath, "*TransmitBuffers", "512");

        // MTU estándar 1500 → JumboPacket 1514.
        SetRegistryString(subKeyPath, "*JumboPacket", "1514");

        // RSS on, Priority+VLAN, LSO v2 IPv4/IPv6.
        SetRegistryString(subKeyPath, "*RSS", "1");
        SetRegistryString(subKeyPath, "*PriorityVLANTag", "3");
        SetRegistryString(subKeyPath, "*LsoV2IPv4", "1");
        SetRegistryString(subKeyPath, "*LsoV2IPv6", "1");

        // Checksum offload TCP/UDP IPv4/IPv6 = 3 (Tx & Rx).
        foreach (var kw in new[]
        {
            "*TCPChecksumOffloadIPv4", "*TCPChecksumOffloadIPv6",
            "*UDPChecksumOffloadIPv4", "*UDPChecksumOffloadIPv6",
        })
            SetRegistryString(subKeyPath, kw, "3");

        // Verificar si SpeedDuplex 1 Gbps llegó a aplicarse; si no, revertir a Auto.
        // Damos un margen y comprobamos LinkSpeed del adaptador.
        Task.Delay(4000).Wait();
        try
        {
            var nic = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(n => n.Id == adapterGuid);
            if (nic is not null && nic.Speed < 1_000_000_000)
                SetRegistryString(subKeyPath, "*SpeedDuplex", "0");
        }
        catch { /* ignora */ }
    }

    /// <summary>Encuentra la subkey de Class\{netGuid}\NNNN cuya NetCfgInstanceId coincida.</summary>
    private static string? FindClassSubKey(string adapterGuid)
    {
        var (root, sub) = SplitHive(NetClassPath);
        using var classKey = root.OpenSubKey(sub);
        if (classKey is null) return null;
        foreach (var name in classKey.GetSubKeyNames())
        {
            if (!int.TryParse(name, out _)) continue;
            using var entry = classKey.OpenSubKey(name);
            var instanceId = entry?.GetValue("NetCfgInstanceId") as string;
            if (string.Equals(instanceId, adapterGuid, StringComparison.OrdinalIgnoreCase))
                return $@"{NetClassPath}\{name}";
        }
        return null;
    }

    // SplitHive replicada localmente porque la base la tiene como private.
    private static (RegistryKey Root, string SubPath) SplitHive(string fullPath)
    {
        var i = fullPath.IndexOf('\\');
        var prefix = i < 0 ? fullPath : fullPath[..i];
        var rest   = i < 0 ? string.Empty : fullPath[(i + 1)..];
        var root = prefix.ToUpperInvariant() switch
        {
            "HKLM" or "HKEY_LOCAL_MACHINE" => Registry.LocalMachine,
            "HKCU" or "HKEY_CURRENT_USER"  => Registry.CurrentUser,
            _ => Registry.LocalMachine,
        };
        return (root, rest);
    }

    /// <summary>
    /// Establece DNS estáticos en cada adaptador Ethernet activo vía WMI.
    /// </summary>
    private static int SetDnsOnEthernetAdapters(string[] dns)
    {
        int applied = 0;
        try
        {
            using var s = new ManagementObjectSearcher(
                "SELECT * FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled = TRUE");
            foreach (ManagementObject nac in s.Get())
            {
                try
                {
                    // Solo Ethernet: comprobamos description / adapter type por nombre.
                    var desc = nac["Description"]?.ToString() ?? string.Empty;
                    // Filtro laxo: la mayoría de Ethernet contienen "Ethernet", "Gigabit", "Realtek", "Intel I", etc.
                    // Para evitar tocar Wi-Fi/Bluetooth nos quedamos con los que NO contengan "Wireless"/"Wi-Fi"/"Bluetooth".
                    if (desc.Contains("Wireless", StringComparison.OrdinalIgnoreCase) ||
                        desc.Contains("Wi-Fi",    StringComparison.OrdinalIgnoreCase) ||
                        desc.Contains("Bluetooth",StringComparison.OrdinalIgnoreCase))
                        continue;

                    var parms = nac.GetMethodParameters("SetDNSServerSearchOrder");
                    parms["DNSServerSearchOrder"] = dns;
                    nac.InvokeMethod("SetDNSServerSearchOrder", parms, null);
                    applied++;
                }
                catch { /* siguiente adaptador */ }
            }
        }
        catch { /* WMI no disponible */ }
        return applied;
    }
}
