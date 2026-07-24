using Aegis.Domain.Controls;
using Aegis.Domain.Findings;
using Aegis.Domain.Snapshot;

namespace Aegis.Controls.Iam;

/// <summary>IAM-005 — application secrets or certificates expiring within 30 days, or without expiration.</summary>
public sealed class ExpiringApplicationCredentialsControl : IControl
{
    public string Id => "IAM-005";
    public string Title => "Application secrets or certificates expiring soon, or without expiration";
    public Severity DefaultSeverity => Severity.High;
    public string? CisReference => "1.2.1";
    public string? MitreTechnique => "T1552.001";

    private static readonly TimeSpan ExpiryWindow = TimeSpan.FromDays(30);
    private readonly TimeProvider _timeProvider;

    public ExpiringApplicationCredentialsControl() : this(TimeProvider.System)
    {
    }

    public ExpiringApplicationCredentialsControl(TimeProvider timeProvider) => _timeProvider = timeProvider;

    public IReadOnlyList<Finding> Evaluate(TenantSnapshot snapshot)
    {
        var now = _timeProvider.GetUtcNow();
        var findings = new List<Finding>();

        foreach (var app in snapshot.Applications)
        {
            foreach (var credential in app.Credentials)
            {
                string? reason = credential.ExpiresOn switch
                {
                    null => "has no expiration date",
                    { } expires when expires <= now => $"expired on {expires:yyyy-MM-dd}",
                    { } expires when expires - now <= ExpiryWindow => $"expires on {expires:yyyy-MM-dd}",
                    _ => null,
                };

                if (reason is null)
                    continue;

                findings.Add(new Finding(
                    ControlId: Id,
                    Severity: DefaultSeverity,
                    ObjectType: "Application",
                    ObjectId: app.Id,
                    ObjectName: app.DisplayName,
                    Evidence: $"{credential.Type} {reason}.",
                    RiskDescription: "Expiring or non-expiring application credentials risk unplanned outages or long-lived attack vectors if leaked.",
                    Remediation:
                    [
                        "Rotate the credential and set an expiration of 12 months or less.",
                        "Prefer certificate-based credentials or workload identity federation over client secrets.",
                    ],
                    CisReference: CisReference,
                    MitreTechnique: MitreTechnique,
                    DetectedAt: now));
            }
        }

        return findings;
    }
}
