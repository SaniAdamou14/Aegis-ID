using System.Text.Json;
using Aegis.Domain.Snapshot;

namespace Aegis.Graph;

/// <summary>
/// Builds a <see cref="TenantSnapshot"/> from live Microsoft Graph calls (US-004).
///
/// Known limitation: break-glass account detection has no Graph-side signal —
/// IsBreakGlassAccount is always false for live collection until a local
/// config file mechanism (akin to the suppressions file idea from US-008)
/// is added to let operators mark known break-glass UPNs.
/// </summary>
public sealed class GraphTenantCollector(GraphHttpClient client)
{
    public async Task<TenantSnapshot> CollectAsync(CancellationToken cancellationToken = default)
    {
        var tenantInfo = await CollectTenantInfoAsync(cancellationToken);
        var users = await CollectUsersAsync(cancellationToken);
        var applications = await CollectApplicationsAsync(cancellationToken);
        var policies = await CollectConditionalAccessPoliciesAsync(cancellationToken);

        return new TenantSnapshot(tenantInfo, users, applications, policies, DateTimeOffset.UtcNow);
    }

    private async Task<TenantInfo> CollectTenantInfoAsync(CancellationToken cancellationToken)
    {
        using var doc = await client.GetJsonAsync("organization?$select=id,displayName", cancellationToken);
        var org = doc.RootElement.GetProperty("value")[0];
        return new TenantInfo(org.GetProperty("id").GetString()!, org.GetProperty("displayName").GetString()!);
    }

    private async Task<IReadOnlyList<AegisUser>> CollectUsersAsync(CancellationToken cancellationToken)
    {
        var users = new Dictionary<string, (string Upn, string DisplayName)>();

        await foreach (var u in client.GetAllPagesAsync("users?$select=id,userPrincipalName,displayName", cancellationToken))
        {
            var id = u.GetProperty("id").GetString()!;
            var upn = u.GetProperty("userPrincipalName").GetString()!;
            users[id] = (upn, u.GetStringOrNull("displayName") ?? upn);
        }

        var rolesByUserId = await CollectDirectoryRoleAssignmentsAsync(users.Keys.ToHashSet(), cancellationToken);
        var methodsByUpn = await CollectAuthenticationMethodsAsync(cancellationToken);

        return users.Select(kv =>
        {
            var (id, (upn, displayName)) = (kv.Key, kv.Value);
            rolesByUserId.TryGetValue(id, out var roles);
            methodsByUpn.TryGetValue(upn, out var methods);

            return new AegisUser(id, upn, displayName, roles ?? [], methods ?? []);
        }).ToList();
    }

    private async Task<Dictionary<string, List<string>>> CollectDirectoryRoleAssignmentsAsync(
        HashSet<string> knownUserIds, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, List<string>>();

        await foreach (var role in client.GetAllPagesAsync("directoryRoles?$select=id,displayName", cancellationToken))
        {
            var roleId = role.GetProperty("id").GetString()!;
            var roleName = role.GetProperty("displayName").GetString()!;

            await foreach (var member in client.GetAllPagesAsync(
                $"directoryRoles/{roleId}/members?$select=id", cancellationToken))
            {
                var memberId = member.GetStringOrNull("id");
                if (memberId is null || !knownUserIds.Contains(memberId))
                    continue; // group or service principal member, not a user we track

                if (!result.TryGetValue(memberId, out var roleNames))
                    result[memberId] = roleNames = [];
                roleNames.Add(roleName);
            }
        }

        return result;
    }

    private async Task<Dictionary<string, List<AuthenticationMethodType>>> CollectAuthenticationMethodsAsync(
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, List<AuthenticationMethodType>>();

        await foreach (var entry in client.GetAllPagesAsync(
            "reports/authenticationMethods/userRegistrationDetails?$select=userPrincipalName,methodsRegistered",
            cancellationToken))
        {
            var upn = entry.GetStringOrNull("userPrincipalName");
            if (upn is null)
                continue;

            result[upn] = entry.GetStringArray("methodsRegistered")
                .Select(MapAuthenticationMethod)
                .Where(m => m is not null)
                .Select(m => m!.Value)
                .Distinct()
                .ToList();
        }

        return result;
    }

    /// <summary>Maps the `/reports/authenticationMethods/userRegistrationDetails` method names we care about.</summary>
    internal static AuthenticationMethodType? MapAuthenticationMethod(string registeredMethod) => registeredMethod switch
    {
        "fido2SecurityKey" or "fido2" => AuthenticationMethodType.Fido2,
        "microsoftAuthenticator" or "microsoftAuthenticatorPush" or "microsoftAuthenticatorPasswordless"
            => AuthenticationMethodType.MicrosoftAuthenticator,
        "windowsHelloForBusiness" => AuthenticationMethodType.WindowsHelloForBusiness,
        "certificate" => AuthenticationMethodType.Certificate,
        "softwareOneTimePasscode" => AuthenticationMethodType.SoftwareOath,
        "mobilePhone" => AuthenticationMethodType.Sms,
        "officePhone" => AuthenticationMethodType.Voice,
        "email" => AuthenticationMethodType.Email,
        _ => null,
    };

    private async Task<IReadOnlyList<AegisApplication>> CollectApplicationsAsync(CancellationToken cancellationToken)
    {
        var apps = new Dictionary<string, (string DisplayName, List<ApplicationCredential> Credentials)>();

        await foreach (var app in client.GetAllPagesAsync(
            "applications?$select=id,appId,displayName,passwordCredentials,keyCredentials", cancellationToken))
        {
            var appId = app.GetProperty("appId").GetString()!;
            var credentials = new List<ApplicationCredential>();

            if (app.TryGetProperty("passwordCredentials", out var pwds))
            {
                foreach (var pwd in pwds.EnumerateArray())
                    credentials.Add(new ApplicationCredential(CredentialType.Secret, pwd.GetDateTimeOffsetOrNull("endDateTime")));
            }

            if (app.TryGetProperty("keyCredentials", out var keys))
            {
                foreach (var key in keys.EnumerateArray())
                    credentials.Add(new ApplicationCredential(CredentialType.Certificate, key.GetDateTimeOffsetOrNull("endDateTime")));
            }

            apps[appId] = (app.GetStringOrNull("displayName") ?? appId, credentials);
        }

        var roleCatalog = await GraphAppRoleCatalog.LoadMicrosoftGraphRolesAsync(client, cancellationToken);
        var graphSp = await GraphServicePrincipalLookup.FindByAppIdAsync(
            client, WellKnownPermissions.MicrosoftGraphAppId, cancellationToken);
        var graphSpId = graphSp?.GetStringOrNull("id");

        var permissionsByAppId = new Dictionary<string, List<string>>();

        await foreach (var sp in client.GetAllPagesAsync(
            "servicePrincipals?$filter=servicePrincipalType eq 'Application'&$expand=appRoleAssignments&$select=appId,appRoleAssignments",
            cancellationToken))
        {
            var appId = sp.GetStringOrNull("appId");
            if (appId is null || !apps.ContainsKey(appId))
                continue;

            var granted = new List<string>();
            if (sp.TryGetProperty("appRoleAssignments", out var assignments))
            {
                foreach (var assignment in assignments.EnumerateArray())
                {
                    if (assignment.GetStringOrNull("resourceId") != graphSpId)
                        continue; // permission granted on a resource other than Microsoft Graph

                    var roleId = assignment.GetStringOrNull("appRoleId");
                    if (roleId is not null && roleCatalog.TryGetValue(roleId, out var name))
                        granted.Add(name);
                }
            }

            permissionsByAppId[appId] = granted;
        }

        return apps.Select(kv =>
        {
            permissionsByAppId.TryGetValue(kv.Key, out var permissions);
            return new AegisApplication(kv.Key, kv.Value.DisplayName, kv.Value.Credentials, permissions ?? []);
        }).ToList();
    }

    private async Task<IReadOnlyList<ConditionalAccessPolicy>> CollectConditionalAccessPoliciesAsync(
        CancellationToken cancellationToken)
    {
        var policies = new List<ConditionalAccessPolicy>();

        await foreach (var p in client.GetAllPagesAsync("identity/conditionalAccess/policies", cancellationToken))
        {
            var conditions = p.GetProperty("conditions");
            var grantControls = p.TryGetProperty("grantControls", out var gc) && gc.ValueKind != JsonValueKind.Null
                ? gc
                : (JsonElement?)null;

            policies.Add(new ConditionalAccessPolicy(
                p.GetProperty("id").GetString()!,
                p.GetProperty("displayName").GetString()!,
                MapState(p.GetProperty("state").GetString()!),
                conditions.GetStringArray("clientAppTypes"),
                grantControls?.GetStringArray("builtInControls") ?? []));
        }

        return policies;
    }

    internal static ConditionalAccessPolicyState MapState(string state) => state switch
    {
        "enabled" => ConditionalAccessPolicyState.Enabled,
        "enabledForReportingButNotEnforced" => ConditionalAccessPolicyState.ReportOnly,
        _ => ConditionalAccessPolicyState.Disabled,
    };
}
