namespace Aegis.Graph;

/// <summary>
/// The exact permission set Aegis-ID requests — all read-only by design.
/// Referenced by both the live collector's own configuration and the
/// `aegis doctor` permission check.
/// </summary>
public static class WellKnownPermissions
{
    public const string MicrosoftGraphAppId = "00000003-0000-0000-c000-000000000000";

    public static readonly IReadOnlyList<string> Required =
    [
        "Directory.Read.All",
        "Policy.Read.All",
        "AuditLog.Read.All",
        "Application.Read.All",
        "RoleManagement.Read.Directory",
        "UserAuthenticationMethod.Read.All",
    ];
}
