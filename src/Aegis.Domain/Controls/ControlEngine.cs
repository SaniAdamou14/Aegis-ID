using System.Reflection;
using Aegis.Domain.Findings;
using Aegis.Domain.Snapshot;

namespace Aegis.Domain.Controls;

/// <summary>
/// Discovers and runs <see cref="IControl"/> implementations. Adding a new
/// control only requires adding a class — no registration step.
/// </summary>
public sealed class ControlEngine
{
    private readonly IReadOnlyList<IControl> _controls;

    public ControlEngine(IEnumerable<IControl> controls) => _controls = controls.ToList();

    public static ControlEngine DiscoverFrom(params Assembly[] assemblies)
    {
        var controls = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => typeof(IControl).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
            .Select(t => (IControl)Activator.CreateInstance(t)!)
            .OrderBy(c => c.Id, StringComparer.Ordinal)
            .ToList();

        return new ControlEngine(controls);
    }

    public ScanResult Run(TenantSnapshot snapshot)
    {
        var results = new List<ControlResult>();

        foreach (var control in _controls)
        {
            try
            {
                var findings = control.Evaluate(snapshot);
                var status = findings.Count > 0 ? ControlStatus.Failed : ControlStatus.Passed;
                results.Add(new ControlResult(control.Id, control.Title, status, findings));
            }
            catch (Exception ex)
            {
                results.Add(new ControlResult(
                    control.Id,
                    control.Title,
                    ControlStatus.Error,
                    Array.Empty<Finding>(),
                    ErrorMessage: ex.Message));
            }
        }

        var allFindings = results.SelectMany(r => r.Findings).ToList();
        var score = PostureScoreCalculator.Calculate(allFindings);

        return new ScanResult(snapshot.TenantInfo.DisplayName, DateTimeOffset.UtcNow, results, score);
    }
}
