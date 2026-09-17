namespace Aegis.Domain.Findings;

/// <summary>Compares two scans' findings by (ControlId, ObjectId) identity — US-018.</summary>
public static class ScanDiffCalculator
{
    public sealed record DiffResult(
        IReadOnlyList<DiffFinding> Appeared,
        IReadOnlyList<DiffFinding> Resolved,
        IReadOnlyList<DiffFinding> Unchanged);

    public static DiffResult Compare(IReadOnlyList<DiffFinding> current, IReadOnlyList<DiffFinding> baseline)
    {
        var baselineKeys = baseline.Select(Key).ToHashSet();
        var currentKeys = current.Select(Key).ToHashSet();

        return new DiffResult(
            Appeared: current.Where(f => !baselineKeys.Contains(Key(f))).ToList(),
            Resolved: baseline.Where(f => !currentKeys.Contains(Key(f))).ToList(),
            Unchanged: current.Where(f => baselineKeys.Contains(Key(f))).ToList());
    }

    private static string Key(DiffFinding finding) => $"{finding.ControlId}::{finding.ObjectId}";
}
