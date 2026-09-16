namespace Aegis.Domain.Snapshot;

public enum ConditionalAccessPolicyState
{
    Enabled,
    Disabled,
    ReportOnly,
}

public sealed record ConditionalAccessPolicy(
    string Id,
    string DisplayName,
    ConditionalAccessPolicyState State,
    IReadOnlyList<string> ClientAppTypes,
    IReadOnlyList<string> GrantControls,
    IReadOnlyList<string>? ExcludedUserIds = null,
    IReadOnlyList<string>? ExcludedGroupIds = null);
