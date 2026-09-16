using Aegis.Domain.Controls;
using Aegis.Domain.Findings;
using Aegis.Domain.Snapshot;

namespace Aegis.Controls.Iam;

/// <summary>IAM-010 — Conditional Access policies left in report-only mode or disabled.</summary>
public sealed class ConditionalAccessPolicyNotEnforcedControl : IControl
{
    public string Id => "IAM-010";
    public string Title => "Conditional Access policies in report-only mode or disabled";
    public Severity DefaultSeverity => Severity.Medium;
    public string? CisReference => "1.2.x";
    public string? MitreTechnique => "T1562.001";

    public IReadOnlyList<Finding> Evaluate(TenantSnapshot snapshot)
    {
        var now = DateTimeOffset.UtcNow;
        var findings = new List<Finding>();

        foreach (var policy in snapshot.ConditionalAccessPolicies)
        {
            if (policy.State == ConditionalAccessPolicyState.Enabled)
                continue;

            var stateDescription = policy.State == ConditionalAccessPolicyState.ReportOnly
                ? "report-only"
                : "disabled";

            findings.Add(new Finding(
                ControlId: Id,
                Severity: DefaultSeverity,
                ObjectType: "ConditionalAccessPolicy",
                ObjectId: policy.Id,
                ObjectName: policy.DisplayName,
                Evidence: $"Policy state is '{stateDescription}'; it does not currently enforce its grant controls.",
                RiskDescription: "A Conditional Access policy that is disabled or in report-only mode provides no actual protection, even though it may appear configured in an audit.",
                Remediation:
                [
                    "Review the policy's sign-in log impact, then switch its state to On.",
                    "If the policy is obsolete, remove it instead of leaving it disabled.",
                ],
                CisReference: CisReference,
                MitreTechnique: MitreTechnique,
                DetectedAt: now));
        }

        return findings;
    }
}
