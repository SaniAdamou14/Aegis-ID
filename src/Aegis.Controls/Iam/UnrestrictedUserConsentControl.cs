using Aegis.Domain.Controls;
using Aegis.Domain.Findings;
using Aegis.Domain.Snapshot;

namespace Aegis.Controls.Iam;

/// <summary>IAM-007 — user consent to applications is unrestricted.</summary>
public sealed class UnrestrictedUserConsentControl : IControl
{
    public string Id => "IAM-007";
    public string Title => "User consent to applications is unrestricted";
    public Severity DefaultSeverity => Severity.High;
    public string? CisReference => "5.1.5";
    public string? MitreTechnique => "T1528";

    public IReadOnlyList<Finding> Evaluate(TenantSnapshot snapshot)
    {
        var policy = snapshot.AuthorizationPolicy;
        if (policy is null || policy.UserConsentForApps != UserConsentPolicy.AllowForAny)
            return [];

        return
        [
            new Finding(
                ControlId: Id,
                Severity: DefaultSeverity,
                ObjectType: "Tenant",
                ObjectId: snapshot.TenantInfo.TenantId,
                ObjectName: snapshot.TenantInfo.DisplayName,
                Evidence: "Tenant authorization policy allows users to consent to any application requesting any permission, including high-risk ones.",
                RiskDescription: "Unrestricted user consent lets an attacker trick a single user into granting a malicious application access to tenant data (consent phishing), without administrator involvement.",
                Remediation:
                [
                    "Restrict user consent to verified publishers and low-risk permissions, or disable user consent entirely.",
                    "Require administrator consent for applications requesting high-risk permissions.",
                ],
                CisReference: CisReference,
                MitreTechnique: MitreTechnique,
                DetectedAt: DateTimeOffset.UtcNow),
        ];
    }
}
