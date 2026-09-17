namespace Aegis.Persistence;

/// <summary>A persisted scan (US-017). <see cref="ReportJson"/> is the same versioned document as <c>--output json</c> — see docs/report-schema.md. No secrets or tokens are ever part of it.</summary>
public sealed class ScanRecord
{
    public Guid Id { get; set; }
    public string TenantDisplayName { get; set; } = "";

    /// <summary>UTC. Stored as <see cref="DateTime"/>, not <see cref="DateTimeOffset"/> — SQLite/EF Core cannot translate DateTimeOffset comparisons server-side.</summary>
    public DateTime EvaluatedAt { get; set; }

    public int PostureScore { get; set; }
    public string ReportJson { get; set; } = "";
}
