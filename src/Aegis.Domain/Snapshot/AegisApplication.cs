namespace Aegis.Domain.Snapshot;

public sealed record AegisApplication(
    string Id,
    string DisplayName,
    IReadOnlyList<ApplicationCredential> Credentials,
    IReadOnlyList<string> GrantedGraphPermissions,
    DateTimeOffset? LastSignInDateTime = null);
