using Aegis.Domain.Controls;
using Aegis.Domain.Findings;
using Aegis.Domain.Snapshot;

namespace Aegis.Controls.Iam;

/// <summary>IAM-012 — application registration allowed for all users.</summary>
public sealed class UnrestrictedApplicationRegistrationControl : IControl
{
    public string Id => "IAM-012";
    public string Title => "Application registration allowed for all users";
    public Severity DefaultSeverity => Severity.Medium;
    public string? CisReference => "5.1.2";
    public string? MitreTechnique => "T1098.001";

    public IReadOnlyList<Finding> Evaluate(TenantSnapshot snapshot)
    {
        var policy = snapshot.AuthorizationPolicy;
        if (policy is null || !policy.UsersCanRegisterApplications)
            return [];

        return
        [
            new Finding(
                ControlId: Id,
                Severity: DefaultSeverity,
                ObjectType: "Tenant",
                ObjectId: snapshot.TenantInfo.TenantId,
                ObjectName: snapshot.TenantInfo.DisplayName,
                Evidence: "Tenant authorization policy allows all users to register new application registrations.",
                RiskDescription: "Unrestricted application registration lets any user create an application and request permissions, expanding the attack surface without administrative oversight.",
                Remediation:
                [
                    "Restrict application registration to administrators or a designated App Developer role.",
                ],
                CisReference: CisReference,
                MitreTechnique: MitreTechnique,
                DetectedAt: DateTimeOffset.UtcNow),
        ];
    }
}
