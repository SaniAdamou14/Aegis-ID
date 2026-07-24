namespace Aegis.Domain.Findings;

public sealed record Finding(
    string ControlId,
    Severity Severity,
    string ObjectType,
    string ObjectId,
    string ObjectName,
    string Evidence,
    string RiskDescription,
    IReadOnlyList<string> Remediation,
    string? CisReference,
    string? MitreTechnique,
    DateTimeOffset DetectedAt,
    bool IsExpectedException = false);
