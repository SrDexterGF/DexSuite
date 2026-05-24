using DexSuite.App.Models;
using Microsoft.EntityFrameworkCore;

namespace DexSuite.App.Data;

/// <summary>
/// DbContext de DexSuite. Por ahora solo aloja el historial interno
/// (<see cref="LogEntry"/>); a futuro alojará puntos de restauración,
/// baselines de rendimiento, etc.
/// </summary>
public sealed class DexSuiteDbContext : DbContext
{
    public DbSet<LogEntry> Logs => Set<LogEntry>();

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
    }
}
