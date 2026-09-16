using Aegis.Controls.Iam;

namespace Aegis.Domain.Tests.Controls;

public class PermanentlyAssignedPrivilegedRolesControlTests
{
    private readonly PermanentlyAssignedPrivilegedRolesControl _control = new();

    [Fact]
    public void NoPermanentAssignment_ProducesNoFinding()
    {
        var snapshot = FixtureLoader.Load("iam-002-compliant.json");

        Assert.Empty(_control.Evaluate(snapshot));
    }

    [Fact]
    public void StandingPrivilegedAssignment_ProducesFinding()
    {
        var snapshot = FixtureLoader.Load("iam-002-permanent-assignment.json");

        var finding = Assert.Single(_control.Evaluate(snapshot));
        Assert.Contains("Global Administrator", finding.Evidence);
    }
}
