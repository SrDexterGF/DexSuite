using System.Management;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using DexSuite.App.Models;

namespace DexSuite.App.Services.CleanupModules;

/// <summary>
/// M11 — Ratón, Teclado y Monitores.
/// Aceleración de ratón off, curva del puntero lineal (REG_BINARY),
/// doble clic rápido, retardo de teclado mínimo y detección de Hz máximos.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class M11Peripherals : ModuleExecutorBase
{
    public M11Peripherals(IChangeTrackingService tracking) : base(tracking) { }

    public override int ModuleId => 11;
    protected override string ModuleName => "Ratón, Teclado y Monitores";

    // Curvas binarias del ratón — copiadas para resultado idéntico al original.
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
        IReadOnlySet<string>? enabledSubOps,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        yield return Header("Ratón, Teclado y Monitores");

        if (Want(enabledSubOps, "M11_mouse_accel"))
        {
            yield return Step("Desactivando aceleración del ratón");
            TrackedSetString(@"HKCU\Control Panel\Mouse", "MouseSpeed", "0");
            TrackedSetString(@"HKCU\Control Panel\Mouse", "MouseThreshold1", "0");
            TrackedSetString(@"HKCU\Control Panel\Mouse", "MouseThreshold2", "0");
            yield return Ok("Aceleración del ratón desactivada");
        }

        if (ct.IsCancellationRequested) yield break;
        if (Want(enabledSubOps, "M11_double_click"))
        {
            yield return Step("Doble clic más rápido (200 ms)");
            TrackedSetString(@"HKCU\Control Panel\Mouse", "DoubleClickSpeed", "200");
            yield return Ok("Doble clic: 200 ms");
        }

        if (ct.IsCancellationRequested) yield break;
        if (Want(enabledSubOps, "M11_keyboard"))
        {
            yield return Step("Teclado: retardo mínimo y velocidad máxima");
            TrackedSetString(@"HKCU\Control Panel\Keyboard", "KeyboardDelay", "0");
            TrackedSetString(@"HKCU\Control Panel\Keyboard", "KeyboardSpeed", "31");
            yield return Ok("Teclado configurado: retardo 0 / velocidad 31");
        }

        if (ct.IsCancellationRequested) yield break;
        if (Want(enabledSubOps, "M11_monitor_hz"))
        {
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
        }

        if (ct.IsCancellationRequested) yield break;
        if (Want(enabledSubOps, "M11_mouse_curve"))
        {
            yield return Step("Curva del puntero lineal (movimiento 1:1)");
            TrackedSetString(@"HKCU\Control Panel\Mouse", "MouseSensitivity", "10");
            TrackedSetBinary(@"HKCU\Control Panel\Mouse", "SmoothMouseXCurve", SmoothMouseXCurve);
            TrackedSetBinary(@"HKCU\Control Panel\Mouse", "SmoothMouseYCurve", SmoothMouseYCurve);
            yield return Ok("Curva del puntero lineal aplicada (sensibilidad 10)");
        }

        // Aplicar cambios del ratón en caliente si se tocó alguna config de ratón.
        if (ct.IsCancellationRequested) yield break;
        if (Want(enabledSubOps, "M11_mouse_accel") || Want(enabledSubOps, "M11_mouse_curve"))
        {
            ApplyMouseSettingsLive();
            yield return Ok("Cambios de ratón aplicados en caliente");
        }

        yield return Done("M11 completado");
        await Task.CompletedTask;
    }
}
