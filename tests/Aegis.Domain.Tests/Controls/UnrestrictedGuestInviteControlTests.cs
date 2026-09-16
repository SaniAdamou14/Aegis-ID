using Aegis.Controls.Iam;

namespace Aegis.Domain.Tests.Controls;

public class UnrestrictedGuestInviteControlTests
{
    private readonly UnrestrictedGuestInviteControl _control = new();

    [Fact]
    public void InviteRestrictedToAdmins_ProducesNoFinding()
    {
        var snapshot = FixtureLoader.Load("iam-014-compliant.json");

        Assert.Empty(_control.Evaluate(snapshot));
    }

    [Fact]
    public void InviteAllowedForEveryone_ProducesFinding()
    {
        var snapshot = FixtureLoader.Load("iam-014-non-compliant.json");

        var finding = Assert.Single(_control.Evaluate(snapshot));
        Assert.Contains("Everyone", finding.Evidence);
    }
}
