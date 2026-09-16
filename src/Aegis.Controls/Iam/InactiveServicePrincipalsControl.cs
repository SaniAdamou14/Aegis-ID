using Aegis.Domain.Controls;
using Aegis.Domain.Findings;
using Aegis.Domain.Snapshot;

namespace Aegis.Controls.Iam;

/// <summary>IAM-008 — service principals with no recent sign-in activity.</summary>
public sealed class InactiveServicePrincipalsControl : IControl
{
    public string Id => "IAM-008";
    public string Title => "Service principals with no sign-in activity for over 90 days";
    public Severity DefaultSeverity => Severity.Medium;
    public string? CisReference => null;
    public string? MitreTechnique => "T1078.004";

    private static readonly TimeSpan InactivityWindow = TimeSpan.FromDays(90);
    private readonly TimeProvider _timeProvider;

    public InactiveServicePrincipalsControl() : this(TimeProvider.System)
    {
    }

    public InactiveServicePrincipalsControl(TimeProvider timeProvider) => _timeProvider = timeProvider;

    // Null LastSignInDateTime means sign-in activity was not collected for this
    // application — it is not flagged, to avoid false positives. Only a confirmed
    // stale timestamp is reported.
    public IReadOnlyList<Finding> Evaluate(TenantSnapshot snapshot)
    {
        var now = _timeProvider.GetUtcNow();
        var findings = new List<Finding>();

        foreach (var app in snapshot.Applications)
        {
            if (app.LastSignInDateTime is not { } lastSignIn || now - lastSignIn <= InactivityWindow)
                continue;

            findings.Add(new Finding(
                ControlId: Id,
                Severity: DefaultSeverity,
                ObjectType: "Application",
                ObjectId: app.Id,
                ObjectName: app.DisplayName,
                Evidence: $"Service principal last signed in on {lastSignIn:yyyy-MM-dd}.",
                RiskDescription: "An inactive service principal that still holds Graph permissions or credentials is unnecessary attack surface — its access is never used but remains exploitable if its credentials leak.",
                Remediation:
                [
                    "Confirm whether the application is still in use.",
                    "Disable or delete the service principal and revoke its credentials if it is no longer needed.",
                ],
                CisReference: CisReference,
                MitreTechnique: MitreTechnique,
                DetectedAt: now));
        }

        return findings;
    }
}
