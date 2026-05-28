using DexSuite.App.Models;
using Microsoft.EntityFrameworkCore;

namespace DexSuite.App.Data;

/// <summary>
/// DbContext de DexSuite. Aloja:
///  - Historial interno (<see cref="LogEntry"/>).
///  - Registro de cambios por módulo (<see cref="ModuleChangeRecord"/>),
///    base para la función "Revertir cambios".
///  - Licencia activa del equipo (<see cref="LicenseEntity"/>) — solo se
///    guarda una fila a la vez, persistida por <c>LicenseService</c>.
/// </summary>
public sealed class DexSuiteDbContext : DbContext
{
    public DbSet<LogEntry> Logs => Set<LogEntry>();
    public DbSet<ModuleChangeRecord> ModuleChanges => Set<ModuleChangeRecord>();
    public DbSet<ModuleStateRecord> ModuleStates => Set<ModuleStateRecord>();
    public DbSet<LicenseEntity> Licenses => Set<LicenseEntity>();

    public DexSuiteDbContext(DbContextOptions<DexSuiteDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var log = modelBuilder.Entity<LogEntry>();
        log.HasKey(e => e.Id);
        log.Property(e => e.TimestampUtc).IsRequired();
        log.Property(e => e.Level).IsRequired();
        log.Property(e => e.Category).IsRequired();
        log.Property(e => e.Message).IsRequired().HasMaxLength(500);

        // Índice por fecha para acelerar el listado "más recientes primero".
        log.HasIndex(e => e.TimestampUtc).IsDescending(true);

        var ch = modelBuilder.Entity<ModuleChangeRecord>();
        ch.HasKey(e => e.Id);
        ch.Property(e => e.ModuleId).IsRequired().HasMaxLength(80);
        ch.Property(e => e.ModuleName).IsRequired().HasMaxLength(200);
        ch.Property(e => e.ChangeType).IsRequired();
        ch.Property(e => e.Target).IsRequired().HasMaxLength(500);
        ch.Property(e => e.AppliedAtUtc).IsRequired();
        // Índices: por módulo (para revertir un módulo entero) + por estado pendiente.
        ch.HasIndex(e => e.ModuleId);
        ch.HasIndex(e => new { e.IsReverted, e.AppliedAtUtc }).IsDescending(false, true);

        var ms = modelBuilder.Entity<ModuleStateRecord>();
        ms.HasKey(e => e.ModuleId);
        ms.Property(e => e.ModuleId).ValueGeneratedNever();
        ms.Property(e => e.IsApplied).IsRequired();

        var lic = modelBuilder.Entity<LicenseEntity>();
        lic.HasKey(e => e.Id);
        lic.Property(e => e.Hwid).IsRequired().HasMaxLength(64);
        lic.Property(e => e.Tier).IsRequired();
        lic.Property(e => e.LicenseId).IsRequired().HasMaxLength(64);
        lic.Property(e => e.Blob).IsRequired();
        lic.Property(e => e.IssuedAtUtc).IsRequired();
        lic.Property(e => e.AppliedAtUtc).IsRequired();
    }
}
