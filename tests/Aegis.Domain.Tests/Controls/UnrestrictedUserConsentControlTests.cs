using Aegis.Controls.Iam;

namespace Aegis.Domain.Tests.Controls;

public class UnrestrictedUserConsentControlTests
{
    private readonly UnrestrictedUserConsentControl _control = new();

    [Fact]
    public void RestrictedConsent_ProducesNoFinding()
    {
        var snapshot = FixtureLoader.Load("iam-007-compliant.json");

        Assert.Empty(_control.Evaluate(snapshot));
    }

    [Fact]
    public void UnrestrictedConsent_ProducesFinding()
    {
        var snapshot = FixtureLoader.Load("iam-007-non-compliant.json");

        var finding = Assert.Single(_control.Evaluate(snapshot));
        Assert.Equal("Tenant", finding.ObjectType);
    }
}
