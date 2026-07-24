namespace Aegis.Graph.Tests;

public class PermissionCheckerTests
{
    private const string Base = "https://graph.microsoft.com/v1.0/";
    private const string GraphAppId = WellKnownPermissions.MicrosoftGraphAppId;
    private const string ClientId = "aegis-client-id";

    private static readonly Dictionary<string, string> GraphCatalogEntry =
        new()
        {
            [$"{Base}servicePrincipals?$filter=appId eq '{GraphAppId}'"] =
                """
                {
                  "value": [
                    {
                      "id": "graph-sp-id",
                      "appRoles": [
                        { "id": "role-directory-read", "value": "Directory.Read.All" },
                        { "id": "role-policy-read", "value": "Policy.Read.All" },
                        { "id": "role-directory-rw", "value": "Directory.ReadWrite.All" }
                      ]
                    }
                  ]
                }
                """,
        };

    private static PermissionChecker CreateChecker(
        string appSpJson, string assignmentsJson, out RoutedHandler handler)
    {
        var responses = new Dictionary<string, string>(GraphCatalogEntry)
        {
            [$"{Base}servicePrincipals?$filter=appId eq '{ClientId}'"] = appSpJson,
            [$"{Base}servicePrincipals/aegis-sp-id/appRoleAssignments"] = assignmentsJson,
        };

        handler = new RoutedHandler(responses);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri(Base) };
        var client = new GraphHttpClient(httpClient, _ => Task.FromResult("token"), new RecordingRetryDelay(), ownsHttpClient: true);
        return new PermissionChecker(client);
    }

    private const string AegisServicePrincipal = """{ "value": [{ "id": "aegis-sp-id" }] }""";

    [Fact]
    public async Task CheckAsync_ReportsGrantedAndMissingPermissions()
    {
        var assignments = """{ "value": [{ "appRoleId": "role-directory-read" }] }""";
        var checker = CreateChecker(AegisServicePrincipal, assignments, out _);

        var report = await checker.CheckAsync(ClientId);

        var directoryRead = report.RequiredPermissions.Single(p => p.Permission == "Directory.Read.All");
        Assert.Equal(PermissionStatus.Granted, directoryRead.Status);

        var policyRead = report.RequiredPermissions.Single(p => p.Permission == "Policy.Read.All");
        Assert.Equal(PermissionStatus.Missing, policyRead.Status);

        Assert.False(report.AllRequiredGranted);
    }

    [Fact]
    public async Task CheckAsync_FlagsUnexpectedWriteScope()
    {
        var assignments = """{ "value": [{ "appRoleId": "role-directory-rw" }] }""";
        var checker = CreateChecker(AegisServicePrincipal, assignments, out _);

        var report = await checker.CheckAsync(ClientId);

        Assert.Contains("Directory.ReadWrite.All", report.UnexpectedWriteScopes);
    }

    [Fact]
    public async Task CheckAsync_NoWriteScopeGranted_ReportsNoWarning()
    {
        var assignments = """{ "value": [{ "appRoleId": "role-directory-read" }, { "appRoleId": "role-policy-read" }] }""";
        var checker = CreateChecker(AegisServicePrincipal, assignments, out _);

        var report = await checker.CheckAsync(ClientId);

        Assert.Empty(report.UnexpectedWriteScopes);
    }

    /// <summary>
    /// NFR-01 guard rail: the exact permission set Aegis-ID requests must be
    /// entirely read-only. This is the automated check the backlog calls for
    /// under US-019 — it fails the build the moment anyone adds a write scope
    /// to WellKnownPermissions.Required.
    /// </summary>
    [Fact]
    public void RequiredPermissions_ContainOnlyReadScopes()
    {
        Assert.All(WellKnownPermissions.Required, permission =>
            Assert.False(PermissionChecker.IsWriteScope(permission), $"'{permission}' is not a read-only scope."));
    }

    [Theory]
    [InlineData("Directory.Read.All", false)]
    [InlineData("Policy.Read.All", false)]
    [InlineData("Directory.ReadWrite.All", true)]
    [InlineData("Mail.Send", true)]
    public void IsWriteScope_ClassifiesCorrectly(string permission, bool expectedIsWrite)
    {
        Assert.Equal(expectedIsWrite, PermissionChecker.IsWriteScope(permission));
    }
}
