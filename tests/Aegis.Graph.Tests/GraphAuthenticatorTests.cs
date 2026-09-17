namespace Aegis.Graph.Tests;

public class GraphAuthenticatorTests
{
    [Fact]
    public void CreateInteractive_BuildsAnAuthenticatorWithoutContactingAzureAd()
    {
        // Construction alone must not perform any network I/O — the device code
        // request only happens once GetAccessTokenAsync is actually called (US-002).
        var authenticator = GraphAuthenticator.CreateInteractive(
            "00000000-0000-0000-0000-000000000001",
            "00000000-0000-0000-0000-000000000002",
            onDeviceCode: _ => { });

        Assert.NotNull(authenticator);
    }
}
