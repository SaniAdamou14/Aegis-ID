using Aegis.Domain.Controls;
using Aegis.Domain.Findings;
using Aegis.Domain.Snapshot;

namespace Aegis.Controls.Iam;

/// <summary>IAM-013 — guest accounts holding a directory role.</summary>
public sealed class GuestAccountWithDirectoryRoleControl : IControl
{
    public string Id => "IAM-013";
    public string Title => "Guest accounts holding a directory role";
    public Severity DefaultSeverity => Severity.High;
    public string? CisReference => "5.1.6";
    public string? MitreTechnique => "T1078.004";

    public IReadOnlyList<Finding> Evaluate(TenantSnapshot snapshot)
    {
        var now = DateTimeOffset.UtcNow;
        var findings = new List<Finding>();

        foreach (var user in snapshot.Users)
        {
            if (user.UserType != AegisUserType.Guest || user.AssignedRoles.Count == 0)
                continue;

            findings.Add(new Finding(
                ControlId: Id,
                Severity: DefaultSeverity,
                ObjectType: "User",
                ObjectId: user.Id,
                ObjectName: user.UserPrincipalName,
                Evidence: $"Guest account holds role(s): {string.Join(", ", user.AssignedRoles)}.",
                RiskDescription: "A guest account with a directory role is managed outside this tenant's identity lifecycle - its compromise in the home tenant directly compromises this one.",
                Remediation:
                [
                    "Remove the directory role from the guest account.",
                    "If administrative access is genuinely required, convert the account to a managed member account subject to this tenant's own security policies.",
                ],
                CisReference: CisReference,
                MitreTechnique: MitreTechnique,
                DetectedAt: now));
        }

        return findings;
    }
}
