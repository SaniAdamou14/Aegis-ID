using Aegis.Controls.Iam;

namespace Aegis.Domain.Tests.Controls;

public class MissingCompliantBreakGlassAccountControlTests
{
    private readonly MissingCompliantBreakGlassAccountControl _control = new();

    [Fact]
    public void NoBreakGlassAccount_ProducesFinding()
    {
        var snapshot = FixtureLoader.Load("iam-004-missing.json");

        var finding = Assert.Single(_control.Evaluate(snapshot));
        Assert.Equal("Tenant", finding.ObjectType);
    }

    [Fact]
    public void BreakGlassAccountWithoutHardwareMfaOrExclusion_ProducesFinding()
    {
        var snapshot = FixtureLoader.Load("iam-004-non-compliant.json");

        var finding = Assert.Single(_control.Evaluate(snapshot));
        Assert.Contains("no hardware-backed authentication method", finding.Evidence);
        Assert.Contains("not excluded", finding.Evidence);
    }

    [Fact]
    public void CompliantBreakGlassAccount_ProducesNoFinding()
    {
        var snapshot = FixtureLoader.Load("iam-004-compliant.json");

        Assert.Empty(_control.Evaluate(snapshot));
    }
}
