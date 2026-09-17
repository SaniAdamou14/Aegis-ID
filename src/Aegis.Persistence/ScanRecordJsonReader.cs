using System.Text.Json;
using Aegis.Domain.Findings;

namespace Aegis.Persistence;

/// <summary>Reads the findings back out of a stored <see cref="ScanRecord.ReportJson"/> for diffing (US-018) — see docs/report-schema.md.</summary>
public static class ScanRecordJsonReader
{
    public static IReadOnlyList<DiffFinding> ReadFindings(string reportJson)
    {
        using var doc = JsonDocument.Parse(reportJson);
        var findings = new List<DiffFinding>();

        foreach (var control in doc.RootElement.GetProperty("controls").EnumerateArray())
        {
            if (!control.TryGetProperty("findings", out var findingsArray))
                continue;

            foreach (var finding in findingsArray.EnumerateArray())
            {
                findings.Add(new DiffFinding(
                    finding.GetProperty("controlId").GetString()!,
                    finding.GetProperty("objectId").GetString()!,
                    finding.GetProperty("objectName").GetString()!,
                    Enum.Parse<Severity>(finding.GetProperty("severity").GetString()!, ignoreCase: true),
                    finding.GetProperty("evidence").GetString()!));
            }
        }

        return findings;
    }
}
