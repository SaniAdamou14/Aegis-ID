using Aegis.Domain.Findings;

namespace Aegis.Domain.Tests;

public class ScanDiffCalculatorTests
{
    private static DiffFinding Finding(string controlId, string objectId, Severity severity = Severity.High) =>
        new(controlId, objectId, $"{objectId}@fixture.test", severity, "evidence");

    [Fact]
    public void FindingOnlyInCurrent_IsAppeared()
    {
        var current = new[] { Finding("IAM-001", "u-1") };
        var baseline = Array.Empty<DiffFinding>();

        var diff = ScanDiffCalculator.Compare(current, baseline);

        Assert.Single(diff.Appeared);
        Assert.Empty(diff.Resolved);
        Assert.Empty(diff.Unchanged);
    }

    [Fact]
    public void FindingOnlyInBaseline_IsResolved()
    {
        var current = Array.Empty<DiffFinding>();
        var baseline = new[] { Finding("IAM-001", "u-1") };

        var diff = ScanDiffCalculator.Compare(current, baseline);

        Assert.Empty(diff.Appeared);
        Assert.Single(diff.Resolved);
        Assert.Empty(diff.Unchanged);
    }

    [Fact]
    public void FindingInBoth_IsUnchanged()
    {
        var current = new[] { Finding("IAM-001", "u-1") };
        var baseline = new[] { Finding("IAM-001", "u-1") };

        var diff = ScanDiffCalculator.Compare(current, baseline);

        Assert.Empty(diff.Appeared);
        Assert.Empty(diff.Resolved);
        Assert.Single(diff.Unchanged);
    }

    [Fact]
    public void IdentityIsControlIdAndObjectId_SeverityChangeStillCountsAsUnchanged()
    {
        var current = new[] { Finding("IAM-001", "u-1", Severity.Critical) };
        var baseline = new[] { Finding("IAM-001", "u-1", Severity.Low) };

        var diff = ScanDiffCalculator.Compare(current, baseline);

        Assert.Single(diff.Unchanged);
    }
}
