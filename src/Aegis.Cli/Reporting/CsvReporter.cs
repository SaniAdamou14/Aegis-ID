using System.Globalization;
using System.Text;
using Aegis.Domain.Findings;

namespace Aegis.Cli.Reporting;

/// <summary>Serializes the findings of a <see cref="ScanResult"/> to CSV, one row per finding (US-011).</summary>
public static class CsvReporter
{
    private static readonly string[] Header =
    [
        "ControlId", "Severity", "ObjectType", "ObjectId", "ObjectName",
        "Evidence", "Remediation", "CisReference", "MitreTechnique", "DetectedAt",
    ];

    public static string Serialize(ScanResult result)
    {
        var sb = new StringBuilder();
        sb.Append('﻿'); // UTF-8 BOM, for Excel compatibility
        sb.Append(string.Join(",", Header)).Append("\r\n");

        foreach (var finding in result.AllFindings)
        {
            string[] fields =
            [
                finding.ControlId,
                finding.Severity.ToString(),
                finding.ObjectType,
                finding.ObjectId,
                finding.ObjectName,
                finding.Evidence,
                string.Join("; ", finding.Remediation),
                finding.CisReference ?? "",
                finding.MitreTechnique ?? "",
                finding.DetectedAt.ToString("O", CultureInfo.InvariantCulture),
            ];

            sb.Append(string.Join(",", fields.Select(Escape))).Append("\r\n");
        }

        return sb.ToString();
    }

    private static string Escape(string field)
    {
        if (field.IndexOfAny([',', '"', '\n', '\r']) < 0)
            return field;

        return $"\"{field.Replace("\"", "\"\"")}\"";
    }
}
