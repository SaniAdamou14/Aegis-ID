using Aegis.Controls.Iam;

namespace Aegis.Domain.Tests.Controls;

public class ExpiringApplicationCredentialsControlTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly ExpiringApplicationCredentialsControl _control = new(new FixedTimeProvider(FixedNow));

    [Fact]
    public void ExpiringAndMissingExpirationCredentials_ProduceFindings()
    {
        var snapshot = FixtureLoader.Load("iam-005-non-compliant.json");

        Assert.Equal(2, _control.Evaluate(snapshot).Count);
    }

    [Fact]
    public void CredentialFarFromExpiry_ProducesNoFinding()
    {
        var snapshot = FixtureLoader.Load("iam-005-compliant.json");

        Assert.Empty(_control.Evaluate(snapshot));
    }
}
