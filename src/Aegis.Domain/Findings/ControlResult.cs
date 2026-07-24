namespace Aegis.Domain.Findings;

public sealed record ControlResult(
    string ControlId,
    string Title,
    ControlStatus Status,
    IReadOnlyList<Finding> Findings,
    string? SkipReason = null,
    string? ErrorMessage = null);
