namespace Aegis.Domain.Findings;

/// <summary>A finding reduced to what a scan-to-scan diff needs (US-018) — identity plus what to show, not the full remediation payload.</summary>
public sealed record DiffFinding(string ControlId, string ObjectId, string ObjectName, Severity Severity, string Evidence)
{
    public static DiffFinding From(Finding finding) =>
        new(finding.ControlId, finding.ObjectId, finding.ObjectName, finding.Severity, finding.Evidence);
}
