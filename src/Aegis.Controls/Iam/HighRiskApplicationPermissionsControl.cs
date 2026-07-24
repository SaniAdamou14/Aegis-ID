using Aegis.Domain.Controls;
using Aegis.Domain.Findings;
using Aegis.Domain.Snapshot;

namespace Aegis.Controls.Iam;

/// <summary>IAM-006 — applications holding high-risk Microsoft Graph permissions.</summary>
public sealed class HighRiskApplicationPermissionsControl : IControl
{
    public string Id => "IAM-006";
    public string Title => "Applications holding high-risk Microsoft Graph permissions";
    public Severity DefaultSeverity => Severity.Critical;
    public string? CisReference => "2.1.1";
    public string? MitreTechnique => "T1098.001";

    public static readonly IReadOnlyList<string> HighRiskPermissions =
    [
        "RoleManagement.ReadWrite.Directory",
        "Directory.ReadWrite.All",
        "Application.ReadWrite.All",
        "AppRoleAssignment.ReadWrite.All",
        "User.ReadWrite.All",
        "Mail.ReadWrite",
        "Mail.Send",
    ];

    public IReadOnlyList<Finding> Evaluate(TenantSnapshot snapshot)
    {
        var now = DateTimeOffset.UtcNow;
        var findings = new List<Finding>();

        foreach (var app in snapshot.Applications)
        {
            var risky = app.GrantedGraphPermissions.Intersect(HighRiskPermissions).ToList();
            if (risky.Count == 0)
                continue;

            findings.Add(new Finding(
                ControlId: Id,
                Severity: DefaultSeverity,
                ObjectType: "Application",
                ObjectId: app.Id,
                ObjectName: app.DisplayName,
                Evidence: $"Granted high-risk permissions: {string.Join(", ", risky)}.",
                RiskDescription: "An application with these permissions can escalate privileges or exfiltrate tenant data if its credentials are compromised.",
                Remediation:
                [
                    "Review whether this application genuinely requires these permissions.",
                    "Replace broad permissions with the narrowest scope that satisfies the use case.",
                ],
                CisReference: CisReference,
                MitreTechnique: MitreTechnique,
                DetectedAt: now));
        }

        return findings;
    }
}
