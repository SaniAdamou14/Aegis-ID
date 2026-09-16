namespace Aegis.Domain.Findings;

public sealed record ScanResult(
    string TenantDisplayName,
    DateTimeOffset EvaluatedAt,
    IReadOnlyList<ControlResult> ControlResults,
    int PostureScore)
{
    public IEnumerable<Finding> AllFindings => ControlResults.SelectMany(r => r.Findings);

    public int CountBySeverity(Severity severity) =>
        AllFindings.Count(f => f.Severity == severity && !f.IsExpectedException && !f.IsSuppressed);
}
