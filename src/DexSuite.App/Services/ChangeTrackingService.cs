using System.Diagnostics;
using System.Runtime.Versioning;
using DexSuite.App.Data;
using DexSuite.App.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace DexSuite.App.Services;

/// <summary>
/// Implementación de <see cref="IChangeTrackingService"/>.
///
/// La persistencia usa el mismo SQLite que el resto de la app (EF Core).
/// La reversión de cada tipo de cambio se delega a Win32:
///   - Registry: Microsoft.Win32.Registry
///   - Servicios: sc.exe config
///   - Tareas: schtasks.exe /Change
///   - Archivos: no soportado todavía (requiere snapshot/backup).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class ChangeTrackingService : IChangeTrackingService
{
    private readonly IDbContextFactory<DexSuiteDbContext> _factory;
    private readonly ILogger<ChangeTrackingService> _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public ChangeTrackingService(
        IDbContextFactory<DexSuiteDbContext> factory,
        ILogger<ChangeTrackingService> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    // -------------------- registrar cambios --------------------

    public Task<int> RecordRegistryChangeAsync(
        string moduleId, string moduleName,
        string keyPath, string? valueName,
        string? originalValue, string? newValue,
        string? valueKind)
        => RecordAsync(new ModuleChangeRecord
        {
            ModuleId      = moduleId,
            ModuleName    = moduleName,
            ChangeType    = ChangeType.RegistryValue,
            Target        = keyPath,
            SubTarget     = valueName,
            OriginalValue = originalValue,
            NewValue      = newValue,
            ValueKind     = valueKind,
            AppliedAtUtc  = DateTime.UtcNow,
        });

    public Task<int> RecordServiceChangeAsync(
        string moduleId, string moduleName,
        string serviceName,
        string? originalStartType, string? newStartType)
        => RecordAsync(new ModuleChangeRecord
        {
            ModuleId      = moduleId,
            ModuleName    = moduleName,
            ChangeType    = ChangeType.ServiceStartup,
            Target        = serviceName,
            OriginalValue = originalStartType,
            NewValue      = newStartType,
            AppliedAtUtc  = DateTime.UtcNow,
        });

    public Task<int> RecordScheduledTaskChangeAsync(
        string moduleId, string moduleName,
        string taskPath,
        string? originalEnabled, string? newEnabled)
        => RecordAsync(new ModuleChangeRecord
        {
            ModuleId      = moduleId,
            ModuleName    = moduleName,
            ChangeType    = ChangeType.ScheduledTask,
            Target        = taskPath,
            OriginalValue = originalEnabled,
            NewValue      = newEnabled,
            AppliedAtUtc  = DateTime.UtcNow,
        });

    public async Task RecordRegistryChangeIfFirstAsync(
        string moduleId, string moduleName,
        string keyPath, string? valueName,
        string? originalValue, string? newValue,
        string? valueKind)
    {
        await _writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await using var db = await _factory.CreateDbContextAsync().ConfigureAwait(false);
            var exists = await db.ModuleChanges.AnyAsync(c =>
                !c.IsReverted &&
                c.ModuleId == moduleId &&
                c.ChangeType == ChangeType.RegistryValue &&
                c.Target == keyPath &&
                c.SubTarget == valueName).ConfigureAwait(false);
            if (exists) return;

            db.ModuleChanges.Add(new ModuleChangeRecord
            {
                ModuleId      = moduleId,
                ModuleName    = moduleName,
                ChangeType    = ChangeType.RegistryValue,
                Target        = keyPath,
                SubTarget     = valueName,
                OriginalValue = originalValue,
                NewValue      = newValue,
                ValueKind     = valueKind,
                AppliedAtUtc  = DateTime.UtcNow,
            });
            await db.SaveChangesAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo registrar el cambio de registro {Key}\\{Value}", keyPath, valueName);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task RecordServiceChangeIfFirstAsync(
        string moduleId, string moduleName,
        string serviceName,
        string? originalStartType, string? newStartType)
    {
        await _writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await using var db = await _factory.CreateDbContextAsync().ConfigureAwait(false);
            var exists = await db.ModuleChanges.AnyAsync(c =>
                !c.IsReverted &&
                c.ModuleId == moduleId &&
                c.ChangeType == ChangeType.ServiceStartup &&
                c.Target == serviceName).ConfigureAwait(false);
            if (exists) return;

            db.ModuleChanges.Add(new ModuleChangeRecord
            {
                ModuleId      = moduleId,
                ModuleName    = moduleName,
                ChangeType    = ChangeType.ServiceStartup,
                Target        = serviceName,
                OriginalValue = originalStartType,
                NewValue      = newStartType,
                AppliedAtUtc  = DateTime.UtcNow,
            });
            await db.SaveChangesAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo registrar el cambio de servicio {Service}", serviceName);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task<int> RecordAsync(ModuleChangeRecord record)
    {
        await _writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await using var db = await _factory.CreateDbContextAsync().ConfigureAwait(false);
            db.ModuleChanges.Add(record);
            await db.SaveChangesAsync().ConfigureAwait(false);
            return record.Id;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    // -------------------- consultar --------------------

    public async Task<IReadOnlyList<ModuleChangeRecord>> GetPendingChangesAsync()
    {
        await using var db = await _factory.CreateDbContextAsync().ConfigureAwait(false);
        return await db.ModuleChanges
            .AsNoTracking()
            .Where(c => !c.IsReverted)
            .OrderByDescending(c => c.AppliedAtUtc)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ModuleChangeRecord>> GetAllChangesAsync(int max = 1000)
    {
        await using var db = await _factory.CreateDbContextAsync().ConfigureAwait(false);
        return await db.ModuleChanges
            .AsNoTracking()
            .OrderByDescending(c => c.AppliedAtUtc)
            .Take(max)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    public async Task<int> CountPendingAsync()
    {
        await using var db = await _factory.CreateDbContextAsync().ConfigureAwait(false);
        return await db.ModuleChanges.CountAsync(c => !c.IsReverted).ConfigureAwait(false);
    }

    // -------------------- revertir --------------------

    public async Task<bool> RevertChangeAsync(int changeId, CancellationToken ct = default)
    {
        ModuleChangeRecord? record;
        await using (var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false))
        {
            record = await db.ModuleChanges.FindAsync([changeId], ct).ConfigureAwait(false);
        }

        if (record is null || record.IsReverted) return false;

        return await DoRevertAsync(record, ct).ConfigureAwait(false);
    }

    public async Task<RevertResult> RevertModuleChangesAsync(string moduleId, CancellationToken ct = default)
    {
        List<ModuleChangeRecord> pending;
        await using (var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false))
        {
            pending = await db.ModuleChanges
                .Where(c => !c.IsReverted && c.ModuleId == moduleId)
                .OrderByDescending(c => c.AppliedAtUtc) // LIFO: revertir el más reciente primero
                .ToListAsync(ct).ConfigureAwait(false);
        }

        return await RevertManyAsync(pending, ct).ConfigureAwait(false);
    }

    public async Task<RevertResult> RevertAllPendingAsync(CancellationToken ct = default)
    {
        List<ModuleChangeRecord> pending;
        await using (var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false))
        {
            pending = await db.ModuleChanges
                .Where(c => !c.IsReverted)
                .OrderByDescending(c => c.AppliedAtUtc)
                .ToListAsync(ct).ConfigureAwait(false);
        }
        return await RevertManyAsync(pending, ct).ConfigureAwait(false);
    }

    private async Task<RevertResult> RevertManyAsync(List<ModuleChangeRecord> records, CancellationToken ct)
    {
        int reverted = 0, failed = 0;
        foreach (var r in records)
        {
            if (ct.IsCancellationRequested) break;
            if (await DoRevertAsync(r, ct).ConfigureAwait(false)) reverted++;
            else failed++;
        }
        return new RevertResult(records.Count, reverted, failed);
    }

    private async Task<bool> DoRevertAsync(ModuleChangeRecord record, CancellationToken ct)
    {
        try
        {
            switch (record.ChangeType)
            {
                case ChangeType.RegistryValue:
                    RevertRegistryValue(record);
                    break;

                case ChangeType.RegistryKey:
                    RevertRegistryKey(record);
                    break;

                case ChangeType.ServiceStartup:
                    await RevertServiceStartupAsync(record, ct).ConfigureAwait(false);
                    break;

                case ChangeType.ScheduledTask:
                    await RevertScheduledTaskAsync(record, ct).ConfigureAwait(false);
                    break;

                case ChangeType.FileSystem:
                    throw new NotSupportedException(
                        "Reversión de cambios de FileSystem aún no implementada (requiere backup).");

                default:
                    throw new ArgumentOutOfRangeException();
            }

            await MarkRevertedAsync(record.Id, error: null).ConfigureAwait(false);
            _logger.LogInformation("Cambio {Id} revertido ({Type} {Target})",
                record.Id, record.ChangeType, record.Target);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo al revertir cambio {Id} ({Type} {Target})",
                record.Id, record.ChangeType, record.Target);
            await MarkRevertedAsync(record.Id, error: ex.Message).ConfigureAwait(false);
            return false;
        }
    }

    private async Task MarkRevertedAsync(int id, string? error)
    {
        await _writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await using var db = await _factory.CreateDbContextAsync().ConfigureAwait(false);
            var entity = await db.ModuleChanges.FindAsync(id).ConfigureAwait(false);
            if (entity is null) return;

            if (error is null)
            {
                entity.IsReverted    = true;
                entity.RevertedAtUtc = DateTime.UtcNow;
                entity.RevertError   = null;
            }
            else
            {
                entity.RevertError = error.Length > 500 ? error[..500] : error;
            }
            await db.SaveChangesAsync().ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    // -------------------- reversión por tipo --------------------

    // Prefijos de registro que los módulos de DexSuite pueden escribir.
    // Cualquier Target que no empiece por uno de estos se rechaza como no confiable.
    private static readonly string[] AllowedRegistryPrefixes =
    [
        @"HKLM\SYSTEM\",
        @"HKLM\SOFTWARE\",
        @"HKCU\Software\",
        @"HKEY_LOCAL_MACHINE\SYSTEM\",
        @"HKEY_LOCAL_MACHINE\SOFTWARE\",
        @"HKEY_CURRENT_USER\Software\",
    ];

    private static void ValidateRegistryPath(string path)
    {
        if (!AllowedRegistryPrefixes.Any(p =>
                path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException($"Ruta de registro no permitida para revertir: {path}");
    }

    // Solo letras, números, guión, guión bajo, punto y espacio.
    // Rechaza comillas y otros chars que romperían los argumentos de sc.exe/schtasks.
    [System.Text.RegularExpressions.GeneratedRegex(@"^[A-Za-z0-9_.\- ]+$")]
    private static partial System.Text.RegularExpressions.Regex ServiceNamePattern();

    // Tareas: igual que servicio más barra invertida y barra normal.
    [System.Text.RegularExpressions.GeneratedRegex(@"^[A-Za-z0-9_.\-\\ ]+$")]
    private static partial System.Text.RegularExpressions.Regex TaskPathPattern();

    private static void ValidateServiceName(string name)
    {
        if (!ServiceNamePattern().IsMatch(name))
            throw new ArgumentException($"Nombre de servicio contiene caracteres no permitidos: {name}");
    }

    private static void ValidateTaskPath(string path)
    {
        if (!TaskPathPattern().IsMatch(path))
            throw new ArgumentException($"Ruta de tarea contiene caracteres no permitidos: {path}");
    }

    /// <summary>
    /// Restaura un valor del registro. Si OriginalValue es null, se elimina el valor
    /// (porque significa que no existía antes del cambio).
    /// </summary>
    private static void RevertRegistryValue(ModuleChangeRecord r)
    {
        ValidateRegistryPath(r.Target);
        var (hive, subKey) = ParseRegistryPath(r.Target);
        using var baseKey = OpenBaseKey(hive);
        using var key = baseKey.CreateSubKey(subKey, writable: true)
                      ?? throw new InvalidOperationException($"No se pudo abrir la clave: {r.Target}");

        var valueName = r.SubTarget ?? string.Empty;

        if (r.OriginalValue is null)
        {
            // El valor no existía antes → eliminarlo.
            key.DeleteValue(valueName, throwOnMissingValue: false);
            return;
        }

        var kind = ParseValueKind(r.ValueKind);
        var data = ConvertToRegistryData(r.OriginalValue, kind);
        key.SetValue(valueName, data, kind);
    }

    /// <summary>
    /// Restaura una clave del registro creando o eliminando según OriginalValue.
    /// OriginalValue = "exists" significa que existía y debe recrearse;
    /// OriginalValue = null significa que no existía y debe eliminarse.
    /// </summary>
    private static void RevertRegistryKey(ModuleChangeRecord r)
    {
        ValidateRegistryPath(r.Target);
        var (hive, subKey) = ParseRegistryPath(r.Target);
        using var baseKey = OpenBaseKey(hive);

        if (string.Equals(r.OriginalValue, "exists", StringComparison.OrdinalIgnoreCase))
            baseKey.CreateSubKey(subKey, writable: true);
        else
            baseKey.DeleteSubKeyTree(subKey, throwOnMissingSubKey: false);
    }

    /// <summary>Cambia el tipo de inicio de un servicio con sc.exe config.</summary>
    private static async Task RevertServiceStartupAsync(ModuleChangeRecord r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.OriginalValue))
            throw new InvalidOperationException("Sin valor original para revertir el servicio.");

        ValidateServiceName(r.Target);

        // sc config <name> start= <auto|demand|disabled|delayed-auto>
        var startType = r.OriginalValue.ToLowerInvariant() switch
        {
            "auto" or "automatic"     => "auto",
            "manual" or "demand"      => "demand",
            "disabled"                => "disabled",
            "delayed-auto" or "delayed" => "delayed-auto",
            _ => throw new InvalidOperationException($"Tipo de inicio desconocido: {r.OriginalValue}"),
        };

        await RunProcessAsync("sc.exe", $"config \"{r.Target}\" start= {startType}", ct)
            .ConfigureAwait(false);
    }

    /// <summary>Habilita o deshabilita una tarea programada con schtasks.exe.</summary>
    private static async Task RevertScheduledTaskAsync(ModuleChangeRecord r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.OriginalValue))
            throw new InvalidOperationException("Sin valor original para revertir la tarea.");

        ValidateTaskPath(r.Target);

        var enable = string.Equals(r.OriginalValue, "true", StringComparison.OrdinalIgnoreCase);
        var verb = enable ? "/Enable" : "/Disable";

        await RunProcessAsync("schtasks.exe", $"/Change /TN \"{r.Target}\" {verb}", ct)
            .ConfigureAwait(false);
    }

    // -------------------- helpers --------------------

    /// <summary>Parsea "HKLM\Software\Foo" → (HKLM, "Software\Foo").</summary>
    private static (string hive, string subKey) ParseRegistryPath(string path)
    {
        var idx = path.IndexOf('\\');
        if (idx < 0) return (path, string.Empty);
        return (path[..idx], path[(idx + 1)..]);
    }

    private static RegistryKey OpenBaseKey(string hive) => hive.ToUpperInvariant() switch
    {
        "HKLM" or "HKEY_LOCAL_MACHINE" => Registry.LocalMachine,
        "HKCU" or "HKEY_CURRENT_USER"  => Registry.CurrentUser,
        "HKCR" or "HKEY_CLASSES_ROOT"  => Registry.ClassesRoot,
        "HKU"  or "HKEY_USERS"         => Registry.Users,
        "HKCC" or "HKEY_CURRENT_CONFIG"=> Registry.CurrentConfig,
        _ => throw new ArgumentException($"Hive desconocido: {hive}"),
    };

    private static RegistryValueKind ParseValueKind(string? kind) => kind?.ToUpperInvariant() switch
    {
        "DWORD"     => RegistryValueKind.DWord,
        "QWORD"     => RegistryValueKind.QWord,
        "SZ"        => RegistryValueKind.String,
        "EXPAND_SZ" => RegistryValueKind.ExpandString,
        "MULTI_SZ"  => RegistryValueKind.MultiString,
        "BINARY"    => RegistryValueKind.Binary,
        _           => RegistryValueKind.String,
    };

    private static object ConvertToRegistryData(string raw, RegistryValueKind kind) => kind switch
    {
        RegistryValueKind.DWord    => int.Parse(raw),
        RegistryValueKind.QWord    => long.Parse(raw),
        RegistryValueKind.MultiString => raw.Split(' ', StringSplitOptions.RemoveEmptyEntries),
        RegistryValueKind.Binary   => Convert.FromBase64String(raw),
        _                          => raw,
    };

    private static async Task RunProcessAsync(string file, string args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName               = file,
            Arguments              = args,
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow         = true,
        };
        using var proc = new Process { StartInfo = psi };
        proc.Start();
        await proc.WaitForExitAsync(ct).ConfigureAwait(false);
        if (proc.ExitCode != 0)
        {
            var err = await proc.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
            throw new InvalidOperationException($"{file} {args} → ExitCode {proc.ExitCode}: {err}");
        }
    }
}
