using Aegis.Domain.Findings;

namespace Aegis.Cli.Reporting;

public static class ConsoleReporter
{
    private static readonly Severity[] SeverityOrder =
    [
        Severity.Critical, Severity.High, Severity.Medium, Severity.Low, Severity.Info,
    ];

    public static void Report(ScanResult result, TextWriter output)
    {
        var useColor = !Console.IsOutputRedirected && Environment.GetEnvironmentVariable("NO_COLOR") is null;

        void WriteColored(string text, ConsoleColor? color)
        {
            if (useColor && color is not null)
                Console.ForegroundColor = color.Value;
            output.WriteLine(text);
            if (useColor && color is not null)
                Console.ResetColor();
        }

        output.WriteLine($"Tenant: {result.TenantDisplayName}");
        output.WriteLine($"Evaluated at: {result.EvaluatedAt:u}");
        output.WriteLine($"Posture score: {result.PostureScore}/100");
        output.WriteLine();

        foreach (var severity in SeverityOrder)
            WriteColored($"{severity}: {result.CountBySeverity(severity)}", SeverityColor(severity));

        output.WriteLine();

        var passed = result.ControlResults.Count(r => r.Status == ControlStatus.Passed);
        var failed = result.ControlResults.Count(r => r.Status == ControlStatus.Failed);
        var skipped = result.ControlResults.Count(r => r.Status == ControlStatus.Skipped);
        var errored = result.ControlResults.Count(r => r.Status == ControlStatus.Error);
        output.WriteLine($"Controls: {passed} passed, {failed} failed, {skipped} skipped, {errored} errored");
        output.WriteLine();

        foreach (var controlResult in result.ControlResults
                     .Where(r => r.Findings.Count > 0)
                     .OrderByDescending(r => r.Findings.Max(f => f.Severity)))
        {
            output.WriteLine($"--- {controlResult.ControlId}: {controlResult.Title} ---");

            foreach (var finding in controlResult.Findings.OrderByDescending(f => f.Severity))
            {
                var tag = finding.IsExpectedException ? " [expected exception]" : "";
                WriteColored(
                    $"  [{finding.Severity}]{tag} {finding.ObjectType} '{finding.ObjectName}': {finding.Evidence}",
                    SeverityColor(finding.Severity));
            }

            output.WriteLine();
        }

        if (errored > 0)
        {
            output.WriteLine("Errors:");
            foreach (var r in result.ControlResults.Where(r => r.Status == ControlStatus.Error))
                output.WriteLine($"  {r.ControlId}: {r.ErrorMessage}");
        }
    }

    private static ConsoleColor SeverityColor(Severity severity) => severity switch
    {
        Severity.Critical => ConsoleColor.Red,
        Severity.High => ConsoleColor.DarkYellow,
        Severity.Medium => ConsoleColor.Yellow,
        Severity.Low => ConsoleColor.Gray,
        _ => ConsoleColor.White,
    };
}
