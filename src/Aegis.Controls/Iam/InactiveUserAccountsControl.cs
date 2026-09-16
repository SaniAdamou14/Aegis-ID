using Aegis.Domain.Controls;
using Aegis.Domain.Findings;
using Aegis.Domain.Snapshot;

namespace Aegis.Controls.Iam;

/// <summary>IAM-015 — active user accounts with no sign-in for over 90 days.</summary>
public sealed class InactiveUserAccountsControl : IControl
{
    public string Id => "IAM-015";
    public string Title => "Active user accounts with no sign-in for over 90 days";
    public Severity DefaultSeverity => Severity.Medium;
    public string? CisReference => "1.1.4";
    public string? MitreTechnique => "T1078.004";

    private static readonly TimeSpan InactivityWindow = TimeSpan.FromDays(90);
    private readonly TimeProvider _timeProvider;

    public InactiveUserAccountsControl() : this(TimeProvider.System)
    {
    }

    public InactiveUserAccountsControl(TimeProvider timeProvider) => _timeProvider = timeProvider;

    // Null LastSignInDateTime means sign-in activity was not collected for this user
    // — it is not flagged, to avoid false positives. Only a confirmed stale
    // timestamp is reported. Break-glass accounts are expected to be dormant.
    public IReadOnlyList<Finding> Evaluate(TenantSnapshot snapshot)
    {
        var now = _timeProvider.GetUtcNow();
        var findings = new List<Finding>();

        foreach (var user in snapshot.Users)
        {
            if (user.IsBreakGlassAccount)
                continue;

            if (user.LastSignInDateTime is not { } lastSignIn || now - lastSignIn <= InactivityWindow)
                continue;

            findings.Add(new Finding(
                ControlId: Id,
                Severity: DefaultSeverity,
                ObjectType: "User",
                ObjectId: user.Id,
                ObjectName: user.UserPrincipalName,
                Evidence: $"Account last signed in on {lastSignIn:yyyy-MM-dd}.",
                RiskDescription: "A stale account with no recent sign-in is unnecessary attack surface: it may retain access to resources and privileged group memberships that are never reviewed.",
                Remediation:
                [
                    "Confirm whether the user is still active in the organization.",
                    "Disable or remove the account if it is no longer needed.",
                ],
                CisReference: CisReference,
                MitreTechnique: MitreTechnique,
                DetectedAt: now));
        }

        return findings;
    }
}
