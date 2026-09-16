using Aegis.Domain.Controls;
using Aegis.Domain.Findings;
using Aegis.Domain.Snapshot;

namespace Aegis.Controls.Iam;

/// <summary>IAM-002 — privileged roles assigned permanently rather than PIM-eligible.</summary>
public sealed class PermanentlyAssignedPrivilegedRolesControl : IControl
{
    public string Id => "IAM-002";
    public string Title => "Privileged roles assigned permanently instead of PIM-eligible";
    public Severity DefaultSeverity => Severity.High;
    public string? CisReference => "1.1.3";
    public string? MitreTechnique => "T1098.003";

    public IReadOnlyList<Finding> Evaluate(TenantSnapshot snapshot)
    {
        var now = DateTimeOffset.UtcNow;
        var findings = new List<Finding>();

        foreach (var user in snapshot.Users)
        {
            var permanentPrivilegedRoles = (user.PermanentAssignmentRoles ?? [])
                .Intersect(PrivilegedAccountsWithoutStrongMfaControl.PrivilegedRoles)
                .ToList();

            if (permanentPrivilegedRoles.Count == 0)
                continue;

            findings.Add(new Finding(
                ControlId: Id,
                Severity: DefaultSeverity,
                ObjectType: "User",
                ObjectId: user.Id,
                ObjectName: user.UserPrincipalName,
                Evidence: $"Standing (non-PIM-eligible) assignment to: {string.Join(", ", permanentPrivilegedRoles)}.",
                RiskDescription: "A permanently assigned privileged role is active around the clock, giving an attacker who compromises the account immediate, unlimited use of that role. A PIM-eligible assignment instead requires a deliberate, logged activation.",
                Remediation:
                [
                    "Convert the standing assignment to a Privileged Identity Management (PIM) eligible assignment.",
                    "Require justification and, where possible, approval for role activation.",
                ],
                CisReference: CisReference,
                MitreTechnique: MitreTechnique,
                DetectedAt: now,
                IsExpectedException: user.IsBreakGlassAccount));
        }

        return findings;
    }
}
