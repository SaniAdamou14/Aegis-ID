using Aegis.Domain.Findings;

namespace Aegis.Domain.Tests;

public class SuppressionApplierTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static Finding SampleFinding() => new(
        "IAM-005", Severity.High, "Application", "app-1", "App One",
        "evidence", "risk", ["step"], null, null, Now);

    private static ScanResult ResultWith(Finding finding) => new(
        "Fixture Tenant", Now,
        [new ControlResult(finding.ControlId, "Title", ControlStatus.Failed, [finding])],
        92);

    [Fact]
    public void ActiveSuppression_MarksFindingSuppressedAndExcludesFromScore()
    {
        var result = ResultWith(SampleFinding());
        var suppressions = new List<Suppression> { new("IAM-005", "app-1", "Documented, rotation scheduled") };

        var applied = SuppressionApplier.Apply(result, suppressions, Now);

        var finding = applied.ScanResult.AllFindings.Single();
        Assert.True(finding.IsSuppressed);
        Assert.Equal("Documented, rotation scheduled", finding.SuppressionReason);
        Assert.Equal(100, applied.ScanResult.PostureScore);
        Assert.Empty(applied.ExpiredSuppressions);
    }

    [Fact]
    public void ExpiredSuppression_DoesNotSuppressFindingAndIsReportedAsExpired()
    {
        var result = ResultWith(SampleFinding());
        var suppressions = new List<Suppression>
        {
            new("IAM-005", "app-1", "Was documented", Now.AddDays(-1)),
        };

        var applied = SuppressionApplier.Apply(result, suppressions, Now);

        var finding = applied.ScanResult.AllFindings.Single();
        Assert.False(finding.IsSuppressed);
        Assert.Equal(92, applied.ScanResult.PostureScore);
        Assert.Single(applied.ExpiredSuppressions);
    }

    [Fact]
    public void NonMatchingSuppression_LeavesFindingUnaffected()
    {
        var result = ResultWith(SampleFinding());
        var suppressions = new List<Suppression> { new("IAM-005", "app-other", "Different object") };

        var applied = SuppressionApplier.Apply(result, suppressions, Now);

        Assert.False(applied.ScanResult.AllFindings.Single().IsSuppressed);
    }

    [Fact]
    public void SuppressedFinding_StillCountsControlAsFailed_NotMergedIntoPassed()
    {
        var result = ResultWith(SampleFinding());
        var suppressions = new List<Suppression> { new("IAM-005", "app-1", "Documented") };

        var applied = SuppressionApplier.Apply(result, suppressions, Now);

        Assert.Equal(ControlStatus.Failed, applied.ScanResult.ControlResults.Single().Status);
    }
}
