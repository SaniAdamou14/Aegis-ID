namespace Aegis.Domain.Findings;

/// <summary>
/// A documented exception for a single (ControlId, ObjectId) pair — US-008.
/// <paramref name="Expires"/> is optional; once passed, the suppression no
/// longer applies and the finding reappears.
/// </summary>
public sealed record Suppression(string ControlId, string ObjectId, string Reason, DateTimeOffset? Expires = null)
{
    public bool IsExpired(DateTimeOffset now) => Expires is { } expires && expires <= now;
}
