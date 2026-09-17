using Aegis.Domain.Findings;
using Aegis.Persistence;

namespace Aegis.Cli.Reporting;

/// <summary>Console output for `aegis diff` (US-018).</summary>
public static class DiffReporter
{
    public static void Report(ScanDiffCalculator.DiffResult diff, ScanRecord current, ScanRecord baseline, TextWriter output)
    {
        output.WriteLine($"Current:  {current.Id} - {current.EvaluatedAt:u} - score {current.PostureScore}/100");
        output.WriteLine($"Baseline: {baseline.Id} - {baseline.EvaluatedAt:u} - score {baseline.PostureScore}/100");
        output.WriteLine();

        WriteSection(output, "Appeared", diff.Appeared);
        WriteSection(output, "Resolved", diff.Resolved);

        output.WriteLine($"Unchanged: {diff.Unchanged.Count}");
    }

    private static void WriteSection(TextWriter output, string title, IReadOnlyList<DiffFinding> findings)
    {
        output.WriteLine($"{title}: {findings.Count}");
        foreach (var finding in findings.OrderByDescending(f => f.Severity))
            output.WriteLine($"  [{finding.Severity}] {finding.ControlId} {finding.ObjectName}: {finding.Evidence}");
        output.WriteLine();
    }
}
