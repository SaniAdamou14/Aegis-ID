using Aegis.Domain.Findings;
using Aegis.Persistence;

namespace Aegis.Persistence.Tests;

public class ScanRecordJsonReaderTests
{
    private static ScanResult SampleResult() => new(
        "Fixture Tenant",
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        [
            new ControlResult(
                "IAM-001", "Title", ControlStatus.Failed,
                [
                    new Finding(
                        "IAM-001", Severity.Critical, "User", "u-1", "ga@fixture.test",
                        "no MFA", "risk", ["step"], null, null,
                        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
                ]),
            new ControlResult("IAM-003", "Passing control", ControlStatus.Passed, []),
        ],
        85);

    [Fact]
    public void ReadFindings_ExtractsEveryFindingAcrossControls()
    {
        var json = JsonReporter.Serialize(SampleResult());

        var findings = ScanRecordJsonReader.ReadFindings(json);

        var finding = Assert.Single(findings);
        Assert.Equal("IAM-001", finding.ControlId);
        Assert.Equal("u-1", finding.ObjectId);
        Assert.Equal(Severity.Critical, finding.Severity);
    }
}
