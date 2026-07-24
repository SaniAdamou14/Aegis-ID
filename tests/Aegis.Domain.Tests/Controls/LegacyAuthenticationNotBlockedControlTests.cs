using Aegis.Controls.Iam;

namespace Aegis.Domain.Tests.Controls;

public class LegacyAuthenticationNotBlockedControlTests
{
    private readonly LegacyAuthenticationNotBlockedControl _control = new();

    [Fact]
    public void ReportOnlyPolicyOnly_ProducesFinding()
    {
        var snapshot = FixtureLoader.Load("iam-009-non-compliant.json");

        Assert.Single(_control.Evaluate(snapshot));
    }

    [Fact]
    public void EnabledBlockingPolicy_ProducesNoFinding()
    {
        var snapshot = FixtureLoader.Load("iam-009-compliant.json");

        Assert.Empty(_control.Evaluate(snapshot));
    }
}
