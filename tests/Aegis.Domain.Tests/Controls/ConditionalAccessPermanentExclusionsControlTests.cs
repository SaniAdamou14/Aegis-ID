using Aegis.Controls.Iam;

namespace Aegis.Domain.Tests.Controls;

public class ConditionalAccessPermanentExclusionsControlTests
{
    private readonly ConditionalAccessPermanentExclusionsControl _control = new();

    [Fact]
    public void OnlyBreakGlassExcluded_ProducesNoFinding()
    {
        var snapshot = FixtureLoader.Load("iam-011-compliant.json");

        Assert.Empty(_control.Evaluate(snapshot));
    }

    [Fact]
    public void NonBreakGlassUserAndGroupExcluded_ProducesFinding()
    {
        var snapshot = FixtureLoader.Load("iam-011-non-compliant.json");

        var finding = Assert.Single(_control.Evaluate(snapshot));
        Assert.Contains("1 user(s) excluded", finding.Evidence);
        Assert.Contains("1 group(s) excluded", finding.Evidence);
    }
}
