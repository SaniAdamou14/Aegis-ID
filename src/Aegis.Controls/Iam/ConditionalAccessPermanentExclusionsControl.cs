using Aegis.Domain.Controls;
using Aegis.Domain.Findings;
using Aegis.Domain.Snapshot;

namespace Aegis.Controls.Iam;

/// <summary>IAM-011 — permanent user or group exclusions from Conditional Access policies.</summary>
public sealed class ConditionalAccessPermanentExclusionsControl : IControl
{
    public string Id => "IAM-011";
    public string Title => "Permanent user or group exclusions in Conditional Access policies";
    public Severity DefaultSeverity => Severity.High;
    public string? CisReference => "1.2.x";
    public string? MitreTechnique => "T1562.001";

    public IReadOnlyList<Finding> Evaluate(TenantSnapshot snapshot)
    {
        var now = DateTimeOffset.UtcNow;
        var breakGlassIds = snapshot.Users.Where(u => u.IsBreakGlassAccount).Select(u => u.Id).ToHashSet();
        var findings = new List<Finding>();

        foreach (var policy in snapshot.ConditionalAccessPolicies.Where(p => p.State == ConditionalAccessPolicyState.Enabled))
        {
            var excludedGroups = policy.ExcludedGroupIds ?? [];
            var nonBreakGlassUsers = (policy.ExcludedUserIds ?? []).Where(id => !breakGlassIds.Contains(id)).ToList();

            // Excluding a documented break-glass account is the expected, recommended
            // state (see IAM-004) — only flag exclusions beyond that.
            if (nonBreakGlassUsers.Count == 0 && excludedGroups.Count == 0)
                continue;

            var evidenceParts = new List<string>();
            if (nonBreakGlassUsers.Count > 0)
                evidenceParts.Add($"{nonBreakGlassUsers.Count} user(s) excluded");
            if (excludedGroups.Count > 0)
                evidenceParts.Add($"{excludedGroups.Count} group(s) excluded");

            findings.Add(new Finding(
                ControlId: Id,
                Severity: DefaultSeverity,
                ObjectType: "ConditionalAccessPolicy",
                ObjectId: policy.Id,
                ObjectName: policy.DisplayName,
                Evidence: string.Join(", ", evidenceParts) + ".",
                RiskDescription: "A permanent exclusion from a Conditional Access policy is a standing bypass of the tenant's access controls for the excluded identities.",
                Remediation:
                [
                    "Limit exclusions to documented break-glass accounts only.",
                    "Replace a group exclusion with a reviewed, time-bound access package instead of a permanent policy exemption.",
                ],
                CisReference: CisReference,
                MitreTechnique: MitreTechnique,
                DetectedAt: now));
        }

        return findings;
    }
}
