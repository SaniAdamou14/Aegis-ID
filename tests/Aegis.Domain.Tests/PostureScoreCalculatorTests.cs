using Aegis.Domain.Findings;

namespace Aegis.Domain.Tests;

public class PostureScoreCalculatorTests
{
    private static Finding MakeFinding(Severity severity, bool expectedException = false) =>
        new("TEST", severity, "Tenant", "t", "t", "evidence", "risk", [], null, null, DateTimeOffset.UtcNow, expectedException);

    [Fact]
    public void NoFindings_ScoreIsMax()
    {
        Assert.Equal(100, PostureScoreCalculator.Calculate([]));
    }

    [Fact]
    public void CriticalFinding_DeductsPenalty()
    {
        Assert.Equal(85, PostureScoreCalculator.Calculate([MakeFinding(Severity.Critical)]));
    }

    [Fact]
    public void ExpectedExceptionFindings_DoNotCountAgainstScore()
    {
        Assert.Equal(100, PostureScoreCalculator.Calculate([MakeFinding(Severity.Critical, expectedException: true)]));
    }

    [Fact]
    public void ManyFindings_ScoreFloorsAtZero()
    {
        var findings = Enumerable.Range(0, 10).Select(_ => MakeFinding(Severity.Critical));

        Assert.Equal(0, PostureScoreCalculator.Calculate(findings));
    }
}
