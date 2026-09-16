using System.Text.Json;
using Aegis.Cli.Reporting;
using Aegis.Domain.Findings;

namespace Aegis.Cli.Tests;

public class JsonReporterTests
{
    private static ScanResult SampleResult() => new(
        "Fixture Tenant",
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        [
            new ControlResult(
                "IAM-001",
                "Privileged accounts without strong MFA registered",
                ControlStatus.Failed,
                [
                    new Finding(
                        "IAM-001", Severity.Critical, "User", "u-1", "ga@fixture.test",
                        "no strong MFA", "risk", ["remediate"], "1.1.1", "T1078.004",
                        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
                ]),
            new ControlResult("IAM-003", "Global Administrator count", ControlStatus.Passed, []),
            new ControlResult("IAM-010", "CA not enforced", ControlStatus.Skipped, [], SkipReason: "Policy.Read.All not granted"),
        ],
        85);

    [Fact]
    public void Serialize_IncludesSchemaVersionAndSeverityCounts()
    {
        var json = JsonReporter.Serialize(SampleResult());
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(JsonReporter.SchemaVersion, root.GetProperty("schemaVersion").GetString());
        Assert.Equal("Fixture Tenant", root.GetProperty("tenant").GetString());
        Assert.Equal(85, root.GetProperty("postureScore").GetInt32());
        Assert.Equal(1, root.GetProperty("severityCounts").GetProperty("critical").GetInt32());
    }

    [Fact]
    public void Serialize_UsesCamelCasePropertyNames()
    {
        var json = JsonReporter.Serialize(SampleResult());
        using var doc = JsonDocument.Parse(json);

        var controls = doc.RootElement.GetProperty("controls");
        var first = controls[0];
        Assert.Equal("IAM-001", first.GetProperty("controlId").GetString());
        Assert.Equal("failed", first.GetProperty("status").GetString());
    }

    [Fact]
    public void Serialize_IncludesSkippedControlWithReason()
    {
        var json = JsonReporter.Serialize(SampleResult());
        using var doc = JsonDocument.Parse(json);

        var skipped = doc.RootElement.GetProperty("controls").EnumerateArray()
            .Single(c => c.GetProperty("controlId").GetString() == "IAM-010");

        Assert.Equal("skipped", skipped.GetProperty("status").GetString());
        Assert.Equal("Policy.Read.All not granted", skipped.GetProperty("skipReason").GetString());
    }
}
