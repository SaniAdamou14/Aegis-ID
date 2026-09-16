using Aegis.Controls.Iam;

namespace Aegis.Domain.Tests.Controls;

public class InactiveUserAccountsControlTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly InactiveUserAccountsControl _control = new(new FixedTimeProvider(FixedNow));

    [Fact]
    public void RecentSignInAndBreakGlass_ProduceNoFinding()
    {
        var snapshot = FixtureLoader.Load("iam-015-compliant.json");

        Assert.Empty(_control.Evaluate(snapshot));
    }

    [Fact]
    public void StaleSignIn_ProducesFinding()
    {
        var snapshot = FixtureLoader.Load("iam-015-non-compliant.json");

        var finding = Assert.Single(_control.Evaluate(snapshot));
        Assert.Contains("2025-01-01", finding.Evidence);
    }
}
