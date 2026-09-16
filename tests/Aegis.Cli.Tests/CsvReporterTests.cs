using Aegis.Cli.Reporting;
using Aegis.Domain.Findings;

namespace Aegis.Cli.Tests;

public class CsvReporterTests
{
    private static ScanResult ResultWith(Finding finding) => new(
        "Fixture Tenant",
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        [new ControlResult(finding.ControlId, "Title", ControlStatus.Failed, [finding])],
        50);

    private static Finding SimpleFinding(string evidence = "evidence") => new(
        "IAM-001", Severity.Critical, "User", "u-1", "ga@fixture.test",
        evidence, "risk", ["step one", "step two"], "1.1.1", "T1078.004",
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Serialize_StartsWithUtf8Bom()
    {
        var csv = CsvReporter.Serialize(ResultWith(SimpleFinding()));

        Assert.Equal('﻿', csv[0]);
    }

    [Fact]
    public void Serialize_WritesHeaderAndOneRowPerFinding()
    {
        var csv = CsvReporter.Serialize(ResultWith(SimpleFinding()));
        var lines = csv.TrimStart('﻿').Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("ControlId,Severity,ObjectType,ObjectId,ObjectName,Evidence,Remediation,CisReference,MitreTechnique,DetectedAt", lines[0]);
        Assert.Equal(2, lines.Length);
        Assert.Contains("IAM-001,Critical,User,u-1,ga@fixture.test", lines[1]);
        Assert.Contains("step one; step two", lines[1]);
    }

    [Fact]
    public void Serialize_EscapesFieldsContainingCommasAndQuotes()
    {
        var finding = SimpleFinding(evidence: "Contains, a comma and a \"quote\".");
        var csv = CsvReporter.Serialize(ResultWith(finding));

        Assert.Contains("\"Contains, a comma and a \"\"quote\"\".\"", csv);
    }

    [Fact]
    public void Serialize_NoFindings_ProducesOnlyHeader()
    {
        var result = new ScanResult("Fixture Tenant", DateTimeOffset.UtcNow, [], 100);

        var csv = CsvReporter.Serialize(result);
        var lines = csv.TrimStart('﻿').Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Single(lines);
    }
}
