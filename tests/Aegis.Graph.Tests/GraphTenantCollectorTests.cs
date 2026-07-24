using Aegis.Domain.Snapshot;

namespace Aegis.Graph.Tests;

public class GraphTenantCollectorTests
{
    private const string Base = "https://graph.microsoft.com/v1.0/";
    private const string GraphAppId = WellKnownPermissions.MicrosoftGraphAppId;

    private static readonly Dictionary<string, string> Responses = new()
    {
        [$"{Base}organization?$select=id,displayName"] =
            """{ "value": [{ "id": "tenant-123", "displayName": "Fixture Tenant" }] }""",

        [$"{Base}users?$select=id,userPrincipalName,displayName"] =
            """
            {
              "value": [
                { "id": "u-1", "userPrincipalName": "alice@fixture.test", "displayName": "Alice" },
                { "id": "u-2", "userPrincipalName": "bob@fixture.test", "displayName": "Bob" }
              ]
            }
            """,

        [$"{Base}directoryRoles?$select=id,displayName"] =
            """{ "value": [{ "id": "role-ga", "displayName": "Global Administrator" }] }""",

        [$"{Base}directoryRoles/role-ga/members?$select=id"] =
            """{ "value": [{ "id": "u-1" }] }""",

        [$"{Base}reports/authenticationMethods/userRegistrationDetails?$select=userPrincipalName,methodsRegistered"] =
            """
            {
              "value": [
                { "userPrincipalName": "alice@fixture.test", "methodsRegistered": [] },
                { "userPrincipalName": "bob@fixture.test", "methodsRegistered": ["fido2SecurityKey", "mobilePhone"] }
              ]
            }
            """,

        [$"{Base}applications?$select=id,appId,displayName,passwordCredentials,keyCredentials"] =
            """
            {
              "value": [
                {
                  "id": "obj-1", "appId": "app-1", "displayName": "Legacy-Sync-Tool",
                  "passwordCredentials": [{ "endDateTime": null }],
                  "keyCredentials": []
                }
              ]
            }
            """,

        [$"{Base}servicePrincipals?$filter=appId eq '{GraphAppId}'"] =
            $$"""
            {
              "value": [
                {
                  "id": "graph-sp-id",
                  "appRoles": [{ "id": "role-directory-rw", "value": "Directory.ReadWrite.All" }]
                }
              ]
            }
            """,

        [$"{Base}servicePrincipals?$filter=servicePrincipalType eq 'Application'&$expand=appRoleAssignments&$select=appId,appRoleAssignments"] =
            """
            {
              "value": [
                {
                  "appId": "app-1",
                  "appRoleAssignments": [
                    { "resourceId": "graph-sp-id", "appRoleId": "role-directory-rw" }
                  ]
                }
              ]
            }
            """,

        [$"{Base}identity/conditionalAccess/policies"] =
            """
            {
              "value": [
                {
                  "id": "ca-1", "displayName": "Block legacy auth", "state": "enabledForReportingButNotEnforced",
                  "conditions": { "clientAppTypes": ["exchangeActiveSync", "other"] },
                  "grantControls": { "builtInControls": ["block"] }
                }
              ]
            }
            """,
    };

    private static GraphTenantCollector CreateCollector(out RoutedHandler handler)
    {
        handler = new RoutedHandler(Responses);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri(Base) };
        var client = new GraphHttpClient(httpClient, _ => Task.FromResult("token"), new RecordingRetryDelay(), ownsHttpClient: true);
        return new GraphTenantCollector(client);
    }

    [Fact]
    public async Task CollectAsync_MapsTenantInfo()
    {
        var collector = CreateCollector(out _);

        var snapshot = await collector.CollectAsync();

        Assert.Equal("tenant-123", snapshot.TenantInfo.TenantId);
        Assert.Equal("Fixture Tenant", snapshot.TenantInfo.DisplayName);
    }

    [Fact]
    public async Task CollectAsync_MapsUsersWithRolesAndAuthMethods()
    {
        var collector = CreateCollector(out _);

        var snapshot = await collector.CollectAsync();

        var alice = snapshot.Users.Single(u => u.UserPrincipalName == "alice@fixture.test");
        Assert.Contains("Global Administrator", alice.AssignedRoles);
        Assert.Empty(alice.AuthenticationMethods);

        var bob = snapshot.Users.Single(u => u.UserPrincipalName == "bob@fixture.test");
        Assert.Empty(bob.AssignedRoles);
        Assert.Contains(AuthenticationMethodType.Fido2, bob.AuthenticationMethods);
        Assert.Contains(AuthenticationMethodType.Sms, bob.AuthenticationMethods);
    }

    [Fact]
    public async Task CollectAsync_MapsApplicationCredentialsAndHighRiskPermissions()
    {
        var collector = CreateCollector(out _);

        var snapshot = await collector.CollectAsync();

        var app = Assert.Single(snapshot.Applications);
        Assert.Equal("Legacy-Sync-Tool", app.DisplayName);
        Assert.Single(app.Credentials);
        Assert.Null(app.Credentials[0].ExpiresOn);
        Assert.Contains("Directory.ReadWrite.All", app.GrantedGraphPermissions);
    }

    [Fact]
    public async Task CollectAsync_MapsReportOnlyConditionalAccessPolicy()
    {
        var collector = CreateCollector(out _);

        var snapshot = await collector.CollectAsync();

        var policy = Assert.Single(snapshot.ConditionalAccessPolicies);
        Assert.Equal(ConditionalAccessPolicyState.ReportOnly, policy.State);
        Assert.Contains("exchangeActiveSync", policy.ClientAppTypes);
        Assert.Contains("block", policy.GrantControls);
    }

    [Theory]
    [InlineData("fido2SecurityKey", AuthenticationMethodType.Fido2)]
    [InlineData("microsoftAuthenticatorPush", AuthenticationMethodType.MicrosoftAuthenticator)]
    [InlineData("windowsHelloForBusiness", AuthenticationMethodType.WindowsHelloForBusiness)]
    [InlineData("certificate", AuthenticationMethodType.Certificate)]
    [InlineData("mobilePhone", AuthenticationMethodType.Sms)]
    [InlineData("officePhone", AuthenticationMethodType.Voice)]
    public void MapAuthenticationMethod_KnownValues_MapCorrectly(string raw, AuthenticationMethodType expected)
    {
        Assert.Equal(expected, GraphTenantCollector.MapAuthenticationMethod(raw));
    }

    [Fact]
    public void MapAuthenticationMethod_UnknownValue_ReturnsNull()
    {
        Assert.Null(GraphTenantCollector.MapAuthenticationMethod("securityQuestion"));
    }
}
