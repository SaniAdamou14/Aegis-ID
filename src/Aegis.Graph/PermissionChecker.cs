namespace Aegis.Graph;

public enum PermissionStatus
{
    Granted,
    Missing,
}

public sealed record PermissionCheckResult(string Permission, PermissionStatus Status);

public sealed record DoctorReport(
    IReadOnlyList<PermissionCheckResult> RequiredPermissions,
    IReadOnlyList<string> UnexpectedWriteScopes)
{
    public bool AllRequiredGranted => RequiredPermissions.All(r => r.Status == PermissionStatus.Granted);
}

/// <summary>Backs `aegis doctor` (US-003): reports which required read scopes are
/// granted, and warns if the app was ever granted a write scope it doesn't need.</summary>
public sealed class PermissionChecker(GraphHttpClient client)
{
    public async Task<DoctorReport> CheckAsync(string clientId, CancellationToken cancellationToken = default)
    {
        var appSp = await GraphServicePrincipalLookup.FindByAppIdAsync(client, clientId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"No service principal found for application '{clientId}' in this tenant. " +
                "Has the app registration been created and consented?");

        var roleCatalog = await GraphAppRoleCatalog.LoadMicrosoftGraphRolesAsync(client, cancellationToken);
        var appSpId = appSp.GetStringOrNull("id")!;

        var grantedNames = new List<string>();
        await foreach (var assignment in client.GetAllPagesAsync(
            $"servicePrincipals/{appSpId}/appRoleAssignments", cancellationToken))
        {
            var roleId = assignment.GetStringOrNull("appRoleId");
            if (roleId is not null && roleCatalog.TryGetValue(roleId, out var name))
                grantedNames.Add(name);
        }

        var results = WellKnownPermissions.Required
            .Select(p => new PermissionCheckResult(
                p, grantedNames.Contains(p) ? PermissionStatus.Granted : PermissionStatus.Missing))
            .ToList();

        var unexpectedWriteScopes = grantedNames
            .Where(IsWriteScope)
            .Distinct()
            .ToList();

        return new DoctorReport(results, unexpectedWriteScopes);
    }

    internal static bool IsWriteScope(string permissionName) =>
        permissionName.Contains("ReadWrite", StringComparison.OrdinalIgnoreCase) ||
        !permissionName.Contains(".Read", StringComparison.OrdinalIgnoreCase);
}
