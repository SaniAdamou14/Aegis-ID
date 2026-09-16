using Aegis.Controls.Iam;

namespace Aegis.Domain.Tests.Controls;

public class InactiveServicePrincipalsControlTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly InactiveServicePrincipalsControl _control = new(new FixedTimeProvider(FixedNow));

    [Fact]
    public void RecentSignInOrNoData_ProducesNoFinding()
    {
        var snapshot = FixtureLoader.Load("iam-008-compliant.json");

        Assert.Empty(_control.Evaluate(snapshot));
    }

    [Fact]
    public void StaleSignIn_ProducesFinding()
    {
        var snapshot = FixtureLoader.Load("iam-008-non-compliant.json");

        var finding = Assert.Single(_control.Evaluate(snapshot));
        Assert.Contains("2025-01-01", finding.Evidence);
    }
}
