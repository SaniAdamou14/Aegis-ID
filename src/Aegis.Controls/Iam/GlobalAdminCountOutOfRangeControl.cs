using Aegis.Domain.Controls;
using Aegis.Domain.Findings;
using Aegis.Domain.Snapshot;

namespace Aegis.Controls.Iam;

/// <summary>IAM-003 — number of Global Administrators outside the recommended 2-4 range.</summary>
public sealed class GlobalAdminCountOutOfRangeControl : IControl
{
    public string Id => "IAM-003";
    public string Title => "Number of Global Administrators outside the 2-4 range";
    public Severity DefaultSeverity => Severity.High;
    public string? CisReference => "1.1.1";
    public string? MitreTechnique => "T1078.004";

    public const string GlobalAdministratorRole = "Global Administrator";
    private const int MinRecommended = 2;
    private const int MaxRecommended = 4;

    public IReadOnlyList<Finding> Evaluate(TenantSnapshot snapshot)
    {
        var globalAdmins = snapshot.Users
            .Where(u => u.AssignedRoles.Contains(GlobalAdministratorRole))
            .ToList();

        if (globalAdmins.Count >= MinRecommended && globalAdmins.Count <= MaxRecommended)
            return [];

        var tooFew = globalAdmins.Count < MinRecommended;
        var names = globalAdmins.Count == 0
            ? "none"
            : string.Join(", ", globalAdmins.Select(u => u.UserPrincipalName));

        var risk = tooFew
            ? "Fewer than 2 Global Administrators creates a single point of failure for tenant recovery."
            : "More than 4 Global Administrators unnecessarily widens the attack surface for the most powerful role in the tenant.";

        return
        [
            new Finding(
                ControlId: Id,
                Severity: DefaultSeverity,
                ObjectType: "Tenant",
                ObjectId: snapshot.TenantInfo.TenantId,
                ObjectName: snapshot.TenantInfo.DisplayName,
                Evidence: $"{globalAdmins.Count} Global Administrator(s) found ({(tooFew ? "too few" : "too many")}): {names}.",
                RiskDescription: risk,
                Remediation:
                [
                    "Keep the number of standing Global Administrators between 2 and 4.",
                    "Move remaining privileged access to Privileged Identity Management (PIM) eligible assignments.",
                ],
                CisReference: CisReference,
                MitreTechnique: MitreTechnique,
                DetectedAt: DateTimeOffset.UtcNow)
        ];
    }
}
