namespace Aegis.Domain.Snapshot;

public sealed record AegisUser(
    string Id,
    string UserPrincipalName,
    string DisplayName,
    IReadOnlyList<string> AssignedRoles,
    IReadOnlyList<AuthenticationMethodType> AuthenticationMethods,
    bool IsBreakGlassAccount = false);
