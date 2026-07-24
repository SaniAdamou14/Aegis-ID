using Aegis.Domain.Controls;
using Aegis.Domain.Findings;
using Aegis.Domain.Snapshot;

namespace Aegis.Controls.Iam;

/// <summary>IAM-009 — legacy authentication protocols not blocked by an active Conditional Access policy.</summary>
public sealed class LegacyAuthenticationNotBlockedControl : IControl
{
    public string Id => "IAM-009";
    public string Title => "Legacy authentication not blocked by an active Conditional Access policy";
    public Severity DefaultSeverity => Severity.Critical;
    public string? CisReference => "1.2.2";
    public string? MitreTechnique => "T1110.003";

    private static readonly string[] LegacyClientAppTypes = ["exchangeActiveSync", "other"];

    public IReadOnlyList<Finding> Evaluate(TenantSnapshot snapshot)
    {
        var blockingPolicyExists = snapshot.ConditionalAccessPolicies.Any(p =>
            p.State == ConditionalAccessPolicyState.Enabled &&
            p.ClientAppTypes.Any(LegacyClientAppTypes.Contains) &&
            p.GrantControls.Contains("block", StringComparer.OrdinalIgnoreCase));

        if (blockingPolicyExists)
            return [];

        return
        [
            new Finding(
                ControlId: Id,
                Severity: DefaultSeverity,
                ObjectType: "Tenant",
                ObjectId: snapshot.TenantInfo.TenantId,
                ObjectName: snapshot.TenantInfo.DisplayName,
                Evidence: "No enabled Conditional Access policy blocks legacy authentication protocols.",
                RiskDescription: "Legacy authentication does not support modern MFA and is a common vector for password-spray attacks.",
                Remediation:
                [
                    "Create a Conditional Access policy targeting client app types 'Exchange ActiveSync' and 'Other clients'.",
                    "Set the policy state to On and the grant control to Block access.",
                ],
                CisReference: CisReference,
                MitreTechnique: MitreTechnique,
                DetectedAt: DateTimeOffset.UtcNow)
        ];
    }
}
