using System.Diagnostics;
using System.Text;
using DexSuite.App.Models;
using Microsoft.Extensions.Logging;

namespace DexSuite.App.Services;

/// <summary>
/// Implementación de <see cref="IBugReportService"/>. Compone un body con
/// información de diagnóstico y abre <c>mailto:suitedex@gmail.com</c> con
/// asunto y cuerpo pre-rellenados.
///
/// Por qué mailto: no requiere SMTP propio (sin credenciales en el binario)
/// ni webhook expuesto (sin riesgo de spam). El usuario revisa y envía
/// el correo desde su propio cliente — total transparencia.
/// </summary>
public sealed class BugReportService : IBugReportService
{
    private const string TargetEmail = "suitedex@gmail.com";
    private const int    LogLinesToInclude = 30;

    private readonly IUpdateService _updateService;
    private readonly ISystemInfoService _systemInfo;
    private readonly IAppLogService _appLog;
    private readonly ILocalizationService _loc;
    private readonly ILogger<BugReportService> _logger;

    public BugReportService(
        IUpdateService updateService,
        ISystemInfoService systemInfo,
        IAppLogService appLog,
        ILocalizationService loc,
        ILogger<BugReportService> logger)
    {
        _updateService = updateService;
        _systemInfo = systemInfo;
        _appLog = appLog;
        _loc = loc;
        _logger = logger;
    }

    public async Task OpenBugReportAsync(string? userDescription = null)
    {
        var subject = $"DexSuite v{_updateService.CurrentVersion} — Bug report";
        var body = await BuildBodyAsync(userDescription).ConfigureAwait(false);

        var mailto = $"mailto:{TargetEmail}" +
                     $"?subject={Uri.EscapeDataString(subject)}" +
                     $"&body={Uri.EscapeDataString(body)}";

        try
        {
            // UseShellExecute=true delega en el handler de mailto: del sistema
            // (Outlook, Gmail web handler, Thunderbird, etc.).
            Process.Start(new ProcessStartInfo
            {
                FileName        = mailto,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo abrir el cliente de correo para el bug report");
            throw;
        }
    }

    public Task OpenSuggestionAsync()
    {
        var subject = $"DexSuite v{_updateService.CurrentVersion} — Sugerencia / Mejora";
        var body    = _loc.Get("Suggestion.Body.DescribePrompt") +
                      $"\n\n\n\n---\nDexSuite v{_updateService.CurrentVersion}";

        var mailto = $"mailto:{TargetEmail}" +
                     $"?subject={Uri.EscapeDataString(subject)}" +
                     $"&body={Uri.EscapeDataString(body)}";

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName        = mailto,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo abrir el cliente de correo para la sugerencia");
            throw;
        }

        return Task.CompletedTask;
    }

    private async Task<string> BuildBodyAsync(string? userDescription)
    {
        var sb = new StringBuilder(capacity: 4096);

        // Descripción del usuario (lo que el usuario escribiría primero).
        sb.AppendLine(_loc.Get("BugReport.Body.DescribePrompt"));
        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(userDescription))
        {
            sb.AppendLine(userDescription);
            sb.AppendLine();
        }
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine(_loc.Get("BugReport.Body.AutoSection"));
        sb.AppendLine("---");
        sb.AppendLine();

        // Bloque de diagnóstico.
        sb.AppendLine($"DexSuite version : {_updateService.CurrentVersion}");
        sb.AppendLine($"Installed build  : {_updateService.IsInstalledBuild}");
        sb.AppendLine($"Language         : {_loc.CurrentLanguage}");
        sb.AppendLine($"Local time       : {DateTime.Now:yyyy-MM-dd HH:mm:ss zzz}");
        sb.AppendLine();

        try
        {
            var sys = await _systemInfo.GetSystemInfoAsync().ConfigureAwait(false);
            sb.AppendLine($"OS               : {sys.OsName} (build {sys.OsBuild})");
            sb.AppendLine($"CPU              : {sys.CpuName} ({sys.CpuCores}c/{sys.CpuLogical}t @ {sys.CpuSpeedMhz} MHz)");
            sb.AppendLine($"GPU              : {sys.GpuName}");
            sb.AppendLine($"RAM              : {sys.RamTotalGb} GB total / {sys.RamAvailableGb} GB free");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"(System info unavailable: {ex.Message})");
        }
        sb.AppendLine();

        // Últimas N entradas del log.
        sb.AppendLine($"--- Last {LogLinesToInclude} log entries ---");
        try
        {
            var entries = await _appLog.GetRecentAsync(LogLinesToInclude).ConfigureAwait(false);
            // Más antiguas primero para lectura cronológica.
            foreach (var e in entries.Reverse())
            {
                sb.AppendLine($"[{e.TimestampUtc.ToLocalTime():HH:mm:ss}] [{e.Level}] [{e.Category}] {e.Message}");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"(Log unavailable: {ex.Message})");
        }

        return sb.ToString();
    }
}
