using Aegis.Domain.Controls;
using Aegis.Domain.Findings;
using Aegis.Domain.Snapshot;

namespace Aegis.Controls.Iam;

/// <summary>IAM-014 — guest invitations allowed for all members.</summary>
public sealed class UnrestrictedGuestInviteControl : IControl
{
    public string Id => "IAM-014";
    public string Title => "Guest invitations allowed for all members";
    public Severity DefaultSeverity => Severity.Medium;
    public string? CisReference => "5.1.6";
    public string? MitreTechnique => "T1136.003";

    private static readonly HashSet<GuestInvitePolicy> NonCompliant =
    [
        GuestInvitePolicy.AdminsInvitersAndMembers,
        GuestInvitePolicy.Everyone,
    ];

    public IReadOnlyList<Finding> Evaluate(TenantSnapshot snapshot)
    {
        var policy = snapshot.AuthorizationPolicy;
        if (policy is null || !NonCompliant.Contains(policy.GuestInviteRestriction))
            return [];

        return
        [
            new Finding(
                ControlId: Id,
                Severity: DefaultSeverity,
                ObjectType: "Tenant",
                ObjectId: snapshot.TenantInfo.TenantId,
                ObjectName: snapshot.TenantInfo.DisplayName,
                Evidence: $"Guest invite restriction is set to '{policy.GuestInviteRestriction}', allowing members to invite guests without administrator involvement.",
                RiskDescription: "Unrestricted guest invitations let any member add external identities to the tenant, which can be used to establish a persistent foothold or exfiltrate data via shared resources.",
                Remediation:
                [
                    "Restrict guest invitations to administrators and users explicitly assigned the Guest Inviter role.",
                ],
                CisReference: CisReference,
                MitreTechnique: MitreTechnique,
                DetectedAt: DateTimeOffset.UtcNow),
        ];
    }
}
