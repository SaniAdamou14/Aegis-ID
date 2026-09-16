namespace Aegis.Domain.Findings;

/// <summary>Applies a suppression list to a <see cref="ScanResult"/> — US-008.</summary>
public static class SuppressionApplier
{
    public sealed record ApplyResult(ScanResult ScanResult, IReadOnlyList<Suppression> ExpiredSuppressions);

    public static ApplyResult Apply(ScanResult result, IReadOnlyList<Suppression> suppressions, DateTimeOffset now)
    {
        var active = suppressions.Where(s => !s.IsExpired(now)).ToList();
        var expired = suppressions.Where(s => s.IsExpired(now)).ToList();

        string? ReasonFor(Finding finding) => active
            .FirstOrDefault(s => s.ControlId == finding.ControlId && s.ObjectId == finding.ObjectId)
            ?.Reason;

        var newControlResults = result.ControlResults
            .Select(controlResult => controlResult with
            {
                Findings = controlResult.Findings
                    .Select(f => ReasonFor(f) is { } reason ? f with { SuppressionReason = reason } : f)
                    .ToList(),
            })
            .ToList();

        var allFindings = newControlResults.SelectMany(r => r.Findings).ToList();
        var score = PostureScoreCalculator.Calculate(allFindings);

        var newResult = result with { ControlResults = newControlResults, PostureScore = score };
        return new ApplyResult(newResult, expired);
    }
}
