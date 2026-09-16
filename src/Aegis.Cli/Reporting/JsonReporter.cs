using System.Text.Json;
using System.Text.Json.Serialization;
using Aegis.Domain.Findings;

namespace Aegis.Cli.Reporting;

/// <summary>Serializes a <see cref="ScanResult"/> to the versioned JSON report schema (US-010). See docs/report-schema.md.</summary>
public static class JsonReporter
{
    public const string SchemaVersion = "1.0";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static string Serialize(ScanResult result)
    {
        var document = new ReportDocument(
            SchemaVersion,
            result.TenantDisplayName,
            result.EvaluatedAt,
            result.PostureScore,
            new SeverityCounts(
                result.CountBySeverity(Severity.Critical),
                result.CountBySeverity(Severity.High),
                result.CountBySeverity(Severity.Medium),
                result.CountBySeverity(Severity.Low),
                result.CountBySeverity(Severity.Info)),
            result.ControlResults
                .Select(r => new ControlReport(r.ControlId, r.Title, r.Status, r.SkipReason, r.ErrorMessage, r.Findings))
                .ToList());

        return JsonSerializer.Serialize(document, Options);
    }

    private sealed record ReportDocument(
        string SchemaVersion,
        string Tenant,
        DateTimeOffset EvaluatedAt,
        int PostureScore,
        SeverityCounts SeverityCounts,
        IReadOnlyList<ControlReport> Controls);

    private sealed record SeverityCounts(int Critical, int High, int Medium, int Low, int Info);

    private sealed record ControlReport(
        string ControlId,
        string Title,
        ControlStatus Status,
        string? SkipReason,
        string? ErrorMessage,
        IReadOnlyList<Finding> Findings);
}
