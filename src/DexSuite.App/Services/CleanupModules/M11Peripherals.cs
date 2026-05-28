using System.Management;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using DexSuite.App.Models;

namespace DexSuite.App.Services.CleanupModules;

/// <summary>
/// M11 — Ratón, Teclado y Monitores.
/// Aceleración de ratón off, curva del puntero lineal (REG_BINARY),
/// doble clic rápido, retardo de teclado mínimo y detección de Hz máximos.
/// Migrado del bloque RUN_11 del .bat.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class M11Peripherals : ModuleExecutorBase
{
    public override int ModuleId => 11;

    // Curvas binarias del ratón — copiadas tal cual del .bat para resultado idéntico.
    private static readonly byte[] SmoothMouseXCurve =
    {
        0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
        0xC0,0xCC,0x0C,0x00,0x00,0x00,0x00,0x00,
        0x80,0x99,0x19,0x00,0x00,0x00,0x00,0x00,
        0x40,0x66,0x26,0x00,0x00,0x00,0x00,0x00,
        0x00,0x33,0x33,0x00,0x00,0x00,0x00,0x00,
    };

    private static readonly byte[] SmoothMouseYCurve =
    {
        0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
        0x00,0x00,0x38,0x00,0x00,0x00,0x00,0x00,
        0x00,0x00,0x70,0x00,0x00,0x00,0x00,0x00,
        0x00,0x00,0xA8,0x00,0x00,0x00,0x00,0x00,
        0x00,0x00,0xE0,0x00,0x00,0x00,0x00,0x00,
    };

    public override async IAsyncEnumerable<ModuleProgress> ExecuteAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        yield return Header("Ratón, Teclado y Monitores");

        // Aceleración del ratón: off.
        yield return Step("Desactivando aceleración del ratón");
        SetRegistryString(@"HKCU\Control Panel\Mouse", "MouseSpeed", "0");
        SetRegistryString(@"HKCU\Control Panel\Mouse", "MouseThreshold1", "0");
        SetRegistryString(@"HKCU\Control Panel\Mouse", "MouseThreshold2", "0");
        yield return Ok("Aceleración del ratón desactivada");

        // Doble clic 200 ms.
        if (ct.IsCancellationRequested) yield break;
        yield return Step("Doble clic más rápido (200 ms)");
        SetRegistryString(@"HKCU\Control Panel\Mouse", "DoubleClickSpeed", "200");
        yield return Ok("Doble clic: 200 ms");

        // Teclado: retardo 0 / velocidad 31.
        if (ct.IsCancellationRequested) yield break;
        yield return Step("Teclado: retardo mínimo y velocidad máxima");
        SetRegistryString(@"HKCU\Control Panel\Keyboard", "KeyboardDelay", "0");
        SetRegistryString(@"HKCU\Control Panel\Keyboard", "KeyboardSpeed", "31");
        yield return Ok("Teclado configurado: retardo 0 / velocidad 31");

        // Detección de Hz por monitor vía WMI.
        if (ct.IsCancellationRequested) yield break;
        yield return Step("Detectando Hz máximos de cada monitor");
        var monitorReports = new List<string>();
        string? monitorErr = null;
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "root\\wmi",
                "SELECT * FROM WmiMonitorListedSupportedSourceModes");
            foreach (ManagementObject m in searcher.Get())
            {
                if (ct.IsCancellationRequested) break;
                int bestHz = 0;
                if (m["MonitorSourceModes"] is ManagementBaseObject[] modes)
                {
                    foreach (var mode in modes)
                    {
                        var num = Convert.ToInt64(mode["VSyncFrequencyNumerator"] ?? 0);
                        var den = Convert.ToInt64(mode["VSyncFrequencyDivisor"] ?? 0);
                        if (den > 0)
                        {
                            var hz = (int)Math.Round((double)num / den);
                            if (hz > bestHz) bestHz = hz;
                        }
                    }
                }
                var inst = m["InstanceName"]?.ToString() ?? "(desconocido)";
                var simpleName = inst.Split('\\').Length > 1 ? inst.Split('\\')[1] : inst;
                monitorReports.Add($"  Monitor: {simpleName} → Max Hz disponible: {bestHz}");
            }
        }
        catch (Exception ex) { monitorErr = ex.Message; }

        if (monitorErr is not null)
            yield return Warn($"No se pudieron leer los datos de monitores: {monitorErr}");
        else if (monitorReports.Count == 0)
            yield return Info("  No se detectaron monitores vía WMI");
        else
            foreach (var r in monitorReports) yield return Info(r);
        yield return Info("Si no estás al máximo: Configuración → Pantalla → Frecuencia de actualización");
        yield return Ok("Detección de Hz completada");

        // Aplicar cambios del ratón en caliente.
        if (ct.IsCancellationRequested) yield break;
        yield return Step("Aplicando cambios del ratón sin reiniciar");
        ApplyMouseSettingsLive();
        yield return Ok("Cambios de ratón aplicados en caliente");

        // Curva del puntero lineal (movimiento 1:1).
        if (ct.IsCancellationRequested) yield break;
        yield return Step("Curva del puntero lineal (movimiento 1:1)");
        SetRegistryString(@"HKCU\Control Panel\Mouse", "MouseSensitivity", "10");
        SetRegistryBinary(@"HKCU\Control Panel\Mouse", "SmoothMouseXCurve", SmoothMouseXCurve);
        SetRegistryBinary(@"HKCU\Control Panel\Mouse", "SmoothMouseYCurve", SmoothMouseYCurve);
        yield return Ok("Curva del puntero lineal aplicada (sensibilidad 10)");

        yield return Done("M11 completado");
        await Task.CompletedTask;
    }
}
