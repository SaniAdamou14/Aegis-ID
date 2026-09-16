using Aegis.Domain.Controls;
using Aegis.Domain.Findings;
using Aegis.Domain.Snapshot;

namespace Aegis.Controls.Iam;

/// <summary>IAM-004 — no break-glass account, or one that isn't fully compliant (hardware MFA, excluded from all Conditional Access policies).</summary>
public sealed class MissingCompliantBreakGlassAccountControl : IControl
{
    public string Id => "IAM-004";
    public string Title => "Missing or non-compliant break-glass account";
    public Severity DefaultSeverity => Severity.High;
    public string? CisReference => "1.1.2";
    public string? MitreTechnique => "T1078.004";

    private static readonly HashSet<AuthenticationMethodType> HardwareMethods =
    [
        AuthenticationMethodType.Fido2,
        AuthenticationMethodType.Certificate,
    ];

    public IReadOnlyList<Finding> Evaluate(TenantSnapshot snapshot)
    {
        var now = DateTimeOffset.UtcNow;
        var breakGlassUsers = snapshot.Users.Where(u => u.IsBreakGlassAccount).ToList();

        if (breakGlassUsers.Count == 0)
        {
            return
            [
                new Finding(
                    ControlId: Id,
                    Severity: DefaultSeverity,
                    ObjectType: "Tenant",
                    ObjectId: snapshot.TenantInfo.TenantId,
                    ObjectName: snapshot.TenantInfo.DisplayName,
                    Evidence: "No account is flagged as a break-glass account in the tenant.",
                    RiskDescription: "Without a compliant break-glass account, an outage of the primary identity provider (Conditional Access misconfiguration, MFA provider failure) can lock out every administrator.",
                    Remediation:
                    [
                        "Create at least two dedicated break-glass accounts, excluded from all Conditional Access policies.",
                        "Register them with a hardware-backed authentication method and store credentials in a physical safe.",
                    ],
                    CisReference: CisReference,
                    MitreTechnique: MitreTechnique,
                    DetectedAt: now),
            ];
        }

        var enabledPolicies = snapshot.ConditionalAccessPolicies
            .Where(p => p.State == ConditionalAccessPolicyState.Enabled)
            .ToList();

        var findings = new List<Finding>();

        foreach (var user in breakGlassUsers)
        {
            var problems = new List<string>();

            if (!user.AuthenticationMethods.Any(HardwareMethods.Contains))
                problems.Add("no hardware-backed authentication method (FIDO2 or certificate) registered");

            var notExcludedFrom = enabledPolicies
                .Where(p => !(p.ExcludedUserIds ?? []).Contains(user.Id))
                .Select(p => p.DisplayName)
                .ToList();

            if (notExcludedFrom.Count > 0)
            {
                var policyWord = notExcludedFrom.Count == 1 ? "policy" : "policies";
                problems.Add($"not excluded from Conditional Access {policyWord}: {string.Join(", ", notExcludedFrom)}");
            }

            if (problems.Count == 0)
                continue;

            findings.Add(new Finding(
                ControlId: Id,
                Severity: DefaultSeverity,
                ObjectType: "User",
                ObjectId: user.Id,
                ObjectName: user.UserPrincipalName,
                Evidence: string.Join("; ", problems) + ".",
                RiskDescription: "A non-compliant break-glass account cannot reliably rescue the tenant during an identity provider outage or a Conditional Access lockout.",
                Remediation:
                [
                    "Exclude the break-glass account from every enabled Conditional Access policy.",
                    "Register a hardware-backed authentication method (FIDO2 security key or certificate) for the account.",
                ],
                CisReference: CisReference,
                MitreTechnique: MitreTechnique,
                DetectedAt: now));
        }

        return findings;
    }
}
