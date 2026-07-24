using Aegis.Domain.Findings;
using Aegis.Domain.Snapshot;

namespace Aegis.Domain.Controls;

public interface IControl
{
    string Id { get; }
    string Title { get; }
    Severity DefaultSeverity { get; }
    string? CisReference { get; }
    string? MitreTechnique { get; }

    IReadOnlyList<Finding> Evaluate(TenantSnapshot snapshot);
}
