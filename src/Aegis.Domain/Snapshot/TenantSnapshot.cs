namespace Aegis.Domain.Snapshot;

public sealed record TenantSnapshot(
    TenantInfo TenantInfo,
    IReadOnlyList<AegisUser> Users,
    IReadOnlyList<AegisApplication> Applications,
    IReadOnlyList<ConditionalAccessPolicy> ConditionalAccessPolicies,
    DateTimeOffset CollectedAt);
