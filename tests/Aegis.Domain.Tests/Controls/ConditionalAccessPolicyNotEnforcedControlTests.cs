using Aegis.Controls.Iam;

namespace Aegis.Domain.Tests.Controls;

public class ConditionalAccessPolicyNotEnforcedControlTests
{
    private readonly ConditionalAccessPolicyNotEnforcedControl _control = new();

    [Fact]
    public void AllPoliciesEnabled_ProducesNoFinding()
    {
        var snapshot = FixtureLoader.Load("iam-010-compliant.json");

        Assert.Empty(_control.Evaluate(snapshot));
    }

    [Fact]
    public void ReportOnlyAndDisabledPolicies_ProduceFindings()
    {
        var snapshot = FixtureLoader.Load("iam-010-non-compliant.json");

        Assert.Equal(2, _control.Evaluate(snapshot).Count);
    }
}
