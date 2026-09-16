using Aegis.Controls.Iam;

namespace Aegis.Domain.Tests.Controls;

public class UnrestrictedApplicationRegistrationControlTests
{
    private readonly UnrestrictedApplicationRegistrationControl _control = new();

    [Fact]
    public void RegistrationRestricted_ProducesNoFinding()
    {
        var snapshot = FixtureLoader.Load("iam-012-compliant.json");

        Assert.Empty(_control.Evaluate(snapshot));
    }

    [Fact]
    public void RegistrationAllowedForAllUsers_ProducesFinding()
    {
        var snapshot = FixtureLoader.Load("iam-012-non-compliant.json");

        var finding = Assert.Single(_control.Evaluate(snapshot));
        Assert.Equal("Tenant", finding.ObjectType);
    }
}
