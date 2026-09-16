namespace Aegis.Domain.Snapshot;

public enum AegisUserType
{
    Member,
    Guest,
}

// PermanentAssignmentRoles: subset of AssignedRoles confirmed as a standing (non-PIM)
// assignment. Null means that distinction was not collected — see IAM-002's known limitation.
public sealed record AegisUser(
    string Id,
    string UserPrincipalName,
    string DisplayName,
    IReadOnlyList<string> AssignedRoles,
    IReadOnlyList<AuthenticationMethodType> AuthenticationMethods,
    bool IsBreakGlassAccount = false,
    AegisUserType UserType = AegisUserType.Member,
    DateTimeOffset? LastSignInDateTime = null,
    IReadOnlyList<string>? PermanentAssignmentRoles = null);
