using Aegis.Domain.Controls;
using Aegis.Domain.Findings;
using Aegis.Domain.Snapshot;

namespace Aegis.Controls.Iam;

/// <summary>IAM-001 — privileged role holders without a strong MFA method registered.</summary>
public sealed class PrivilegedAccountsWithoutStrongMfaControl : IControl
{
    public string Id => "IAM-001";
    public string Title => "Privileged accounts without strong MFA registered";
    public Severity DefaultSeverity => Severity.Critical;
    public string? CisReference => "1.1.1";
    public string? MitreTechnique => "T1078.004";

    public static readonly IReadOnlyList<string> PrivilegedRoles = new[]
    {
        "Global Administrator",
        "Privileged Role Administrator",
        "Security Administrator",
        "Exchange Administrator",
        "SharePoint Administrator",
        "User Administrator",
        "Application Administrator",
        "Cloud Application Administrator",
        "Authentication Administrator",
        "Helpdesk Administrator",
    };

    private static readonly HashSet<AuthenticationMethodType> StrongMethods =
    [
        AuthenticationMethodType.Fido2,
        AuthenticationMethodType.MicrosoftAuthenticator,
        AuthenticationMethodType.WindowsHelloForBusiness,
        AuthenticationMethodType.Certificate,
    ];

    private static readonly HashSet<AuthenticationMethodType> PhoneMethods =
    [
        AuthenticationMethodType.Sms,
        AuthenticationMethodType.Voice,
    ];

    public IReadOnlyList<Finding> Evaluate(TenantSnapshot snapshot)
    {
        var now = DateTimeOffset.UtcNow;
        var findings = new List<Finding>();

        foreach (var user in snapshot.Users)
        {
            var privilegedRoles = user.AssignedRoles.Intersect(PrivilegedRoles).ToList();
            if (privilegedRoles.Count == 0)
                continue;

            if (user.AuthenticationMethods.Any(StrongMethods.Contains))
                continue;

            var phoneOnly = user.AuthenticationMethods.Any(PhoneMethods.Contains);
            var severity = phoneOnly ? Severity.High : Severity.Critical;

            var methodsDescription = user.AuthenticationMethods.Count == 0
                ? "no authentication method registered"
                : string.Join(", ", user.AuthenticationMethods);

            var risk = phoneOnly
                ? "Privileged account protected only by phone-based MFA (SMS/voice), which is vulnerable to SIM-swap and MFA fatigue attacks."
                : "Privileged account has no strong MFA method registered, making it a prime target for credential compromise.";

            findings.Add(new Finding(
                ControlId: Id,
                Severity: severity,
                ObjectType: "User",
                ObjectId: user.Id,
                ObjectName: user.UserPrincipalName,
                Evidence: $"Roles: {string.Join(", ", privilegedRoles)}. Registered methods: {methodsDescription}.",
                RiskDescription: risk,
                Remediation:
                [
                    "Register a FIDO2 security key, Windows Hello for Business, or the Microsoft Authenticator app for this account.",
                    "Enforce phishing-resistant MFA for all privileged roles via a Conditional Access authentication strengths policy.",
                ],
                CisReference: CisReference,
                MitreTechnique: MitreTechnique,
                DetectedAt: now,
                IsExpectedException: user.IsBreakGlassAccount));
        }

        return findings;
    }
}
