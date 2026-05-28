using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DexSuite.App.Services.Licensing;

/// <summary>
/// Servicio en segundo plano que re-verifica la licencia periódicamente.
///
/// • Tick base: 10 minutos.
/// • Jitter aleatorio ±60 segundos para que un atacante no pueda sincronizar
///   sus parches con la ventana de pausa.
/// • Si <see cref="ILicenseService.RevalidateAsync"/> falla → el servicio ya
///   pone el tier a Free internamente y emite <c>TierChanged</c>; aquí solo
///   loggeamos.
///
/// Hospedado por <c>Host.CreateDefaultBuilder().UseSerilog()</c> en App.xaml.cs.
/// </summary>
public sealed class LicenseWatchdog : BackgroundService
{
    private readonly ILicenseService _license;
    private readonly ILogger<LicenseWatchdog> _logger;

    private static readonly TimeSpan BaseInterval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan JitterRange  = TimeSpan.FromSeconds(60);

    public LicenseWatchdog(ILicenseService license, ILogger<LicenseWatchdog> logger)
    {
        _license = license;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Primer tick: pequeña espera para no chocar con el arranque (que ya
        // hace su propia revalidación inicial al construir el host).
        try { await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken).ConfigureAwait(false); }
        catch (OperationCanceledException) { return; }

        var rng = new Random();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = await _license.RevalidateAsync(stoppingToken).ConfigureAwait(false);
                if (!result.Success)
                    _logger.LogWarning("Watchdog: revalidación falló — {Reason}", result.Message);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Watchdog: excepción durante revalidación");
            }

            // Tick + jitter en [-60s, +60s].
            var jitterMs = rng.Next(-(int)JitterRange.TotalMilliseconds, (int)JitterRange.TotalMilliseconds);
            var wait = BaseInterval + TimeSpan.FromMilliseconds(jitterMs);
            try { await Task.Delay(wait, stoppingToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
        }
    }
}
