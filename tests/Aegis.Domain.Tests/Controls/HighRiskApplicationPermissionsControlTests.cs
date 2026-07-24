using Aegis.Controls.Iam;
using Aegis.Domain.Findings;

namespace Aegis.Domain.Tests.Controls;

public class HighRiskApplicationPermissionsControlTests
{
    private readonly HighRiskApplicationPermissionsControl _control = new();

    [Fact]
    public void AppWithHighRiskPermission_ProducesCriticalFinding()
    {
        var snapshot = FixtureLoader.Load("iam-006-non-compliant.json");

        var finding = Assert.Single(_control.Evaluate(snapshot));
        Assert.Equal(Severity.Critical, finding.Severity);
    }

    [Fact]
    public void AppWithLowRiskPermissions_ProducesNoFinding()
    {
        var snapshot = FixtureLoader.Load("iam-006-compliant.json");

        Assert.Empty(_control.Evaluate(snapshot));
    }
}
