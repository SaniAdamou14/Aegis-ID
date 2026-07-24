using Aegis.Controls.Iam;

namespace Aegis.Domain.Tests.Controls;

public class GlobalAdminCountOutOfRangeControlTests
{
    private readonly GlobalAdminCountOutOfRangeControl _control = new();

    [Fact]
    public void CountWithinRange_ProducesNoFinding()
    {
        var snapshot = FixtureLoader.Load("iam-003-compliant.json");

        Assert.Empty(_control.Evaluate(snapshot));
    }

    [Fact]
    public void TooFewAdmins_ProducesFinding()
    {
        var snapshot = FixtureLoader.Load("iam-003-too-few.json");

        var finding = Assert.Single(_control.Evaluate(snapshot));
        Assert.Contains("too few", finding.Evidence);
    }

    [Fact]
    public void TooManyAdmins_ProducesFinding()
    {
        var snapshot = FixtureLoader.Load("iam-003-too-many.json");

        var finding = Assert.Single(_control.Evaluate(snapshot));
        Assert.Contains("too many", finding.Evidence);
    }
}
