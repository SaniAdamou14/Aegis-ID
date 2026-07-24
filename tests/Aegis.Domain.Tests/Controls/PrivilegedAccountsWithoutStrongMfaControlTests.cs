using Aegis.Controls.Iam;
using Aegis.Domain.Findings;

namespace Aegis.Domain.Tests.Controls;

public class PrivilegedAccountsWithoutStrongMfaControlTests
{
    private readonly PrivilegedAccountsWithoutStrongMfaControl _control = new();

    [Fact]
    public void CompliantAdmin_ProducesNoFinding()
    {
        var snapshot = FixtureLoader.Load("iam-001-compliant.json");

        Assert.Empty(_control.Evaluate(snapshot));
    }

    [Fact]
    public void AdminWithoutMfa_ProducesCriticalFinding()
    {
        var snapshot = FixtureLoader.Load("iam-001-no-mfa.json");

        var finding = Assert.Single(_control.Evaluate(snapshot));
        Assert.Equal(Severity.Critical, finding.Severity);
        Assert.False(finding.IsExpectedException);
    }

    [Fact]
    public void AdminWithPhoneOnlyMfa_ProducesHighFinding()
    {
        var snapshot = FixtureLoader.Load("iam-001-weak-mfa.json");

        var finding = Assert.Single(_control.Evaluate(snapshot));
        Assert.Equal(Severity.High, finding.Severity);
    }

    [Fact]
    public void BreakGlassAccountWithoutMfa_ProducesExpectedExceptionFinding()
    {
        var snapshot = FixtureLoader.Load("iam-001-breakglass.json");

        var finding = Assert.Single(_control.Evaluate(snapshot));
        Assert.True(finding.IsExpectedException);
    }
}
