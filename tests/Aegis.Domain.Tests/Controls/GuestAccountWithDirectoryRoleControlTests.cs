using Aegis.Controls.Iam;

namespace Aegis.Domain.Tests.Controls;

public class GuestAccountWithDirectoryRoleControlTests
{
    private readonly GuestAccountWithDirectoryRoleControl _control = new();

    [Fact]
    public void GuestWithoutRole_ProducesNoFinding()
    {
        var snapshot = FixtureLoader.Load("iam-013-compliant.json");

        Assert.Empty(_control.Evaluate(snapshot));
    }

    [Fact]
    public void GuestWithDirectoryRole_ProducesFinding()
    {
        var snapshot = FixtureLoader.Load("iam-013-non-compliant.json");

        var finding = Assert.Single(_control.Evaluate(snapshot));
        Assert.Contains("Helpdesk Administrator", finding.Evidence);
    }
}
