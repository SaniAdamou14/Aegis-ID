using Microsoft.EntityFrameworkCore;

namespace Aegis.Persistence;

public sealed class AegisDbContext(DbContextOptions<AegisDbContext> options) : DbContext(options)
{
    public DbSet<ScanRecord> ScanRecords => Set<ScanRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ScanRecord>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.Property(r => r.TenantDisplayName).IsRequired();
            entity.Property(r => r.ReportJson).IsRequired();
            entity.HasIndex(r => r.EvaluatedAt);
        });
    }
}
