using Aegis.Domain.Findings;
using Microsoft.EntityFrameworkCore;

namespace Aegis.Persistence;

/// <summary>Persists and retrieves scan history (US-017), applying retention on every save.</summary>
public sealed class ScanHistoryStore(AegisDbContext db)
{
    public static readonly TimeSpan DefaultRetention = TimeSpan.FromDays(90);

    public async Task<ScanRecord> SaveAsync(ScanResult result, TimeSpan? retention = null, CancellationToken cancellationToken = default)
    {
        var record = new ScanRecord
        {
            Id = Guid.NewGuid(),
            TenantDisplayName = result.TenantDisplayName,
            EvaluatedAt = result.EvaluatedAt.UtcDateTime,
            PostureScore = result.PostureScore,
            ReportJson = JsonReporter.Serialize(result),
        };

        db.ScanRecords.Add(record);

        var cutoff = DateTime.UtcNow - (retention ?? DefaultRetention);
        var stale = await db.ScanRecords.Where(r => r.EvaluatedAt < cutoff).ToListAsync(cancellationToken);
        db.ScanRecords.RemoveRange(stale);

        await db.SaveChangesAsync(cancellationToken);
        return record;
    }

    public Task<ScanRecord?> GetLatestAsync(CancellationToken cancellationToken = default) =>
        db.ScanRecords.OrderByDescending(r => r.EvaluatedAt).FirstOrDefaultAsync(cancellationToken);

    public Task<ScanRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.ScanRecords.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<List<ScanRecord>> GetRecentAsync(int count, CancellationToken cancellationToken = default) =>
        db.ScanRecords.OrderByDescending(r => r.EvaluatedAt).Take(count).ToListAsync(cancellationToken);
}
