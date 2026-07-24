namespace Aegis.Domain.Findings;

/// <summary>
/// Score = 100 minus the sum of per-finding severity penalties, floored at 0.
/// Findings marked as an expected exception (e.g. a documented break-glass
/// account) do not count against the score.
/// </summary>
public static class PostureScoreCalculator
{
    private static readonly IReadOnlyDictionary<Severity, int> Penalties = new Dictionary<Severity, int>
    {
        [Severity.Critical] = 15,
        [Severity.High] = 8,
        [Severity.Medium] = 4,
        [Severity.Low] = 1,
        [Severity.Info] = 0,
    };

    public static int Calculate(IEnumerable<Finding> findings)
    {
        var penalty = findings
            .Where(f => !f.IsExpectedException)
            .Sum(f => Penalties[f.Severity]);

        return Math.Max(0, 100 - penalty);
    }
}
