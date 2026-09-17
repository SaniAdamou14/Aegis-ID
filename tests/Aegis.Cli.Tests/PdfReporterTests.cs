using System.Diagnostics;
using Aegis.Cli.Reporting;
using Aegis.Domain.Findings;

namespace Aegis.Cli.Tests;

public class PdfReporterTests
{
    private static Finding SampleFinding(string objectId, Severity severity) => new(
        "IAM-001", severity, "User", objectId, $"{objectId}@fixture.test",
        "evidence", "risk", ["step one", "step two"], "1.1.1", "T1078.004",
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    private static ScanResult ResultWith(IReadOnlyList<Finding> findings) => new(
        "Fixture Tenant",
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        [new ControlResult("IAM-001", "Title", ControlStatus.Failed, findings)],
        50);

    private static string TempPdfPath() => Path.Combine(Path.GetTempPath(), $"aegis-report-{Guid.NewGuid():N}.pdf");

    [Fact]
    public void GenerateFile_WritesAValidPdf()
    {
        var path = TempPdfPath();
        try
        {
            PdfReporter.GenerateFile(ResultWith([SampleFinding("u-1", Severity.High)]), path);

            Assert.True(File.Exists(path));
            var bytes = File.ReadAllBytes(path);
            Assert.True(bytes.Length > 0);
            Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        }
        finally
        {
            File.Delete(path);
        }
    }

    // Regression test: when every finding of a severity shares the same (topmost) count as the
    // chart's max, the "remaining space" bar segment's relative width hit exactly zero and
    // QuestPDF's RelativeItem threw ArgumentOutOfRangeException ("size must be greater than zero").
    [Fact]
    public void GenerateFile_SeverityAtChartMaximum_DoesNotThrow()
    {
        var path = TempPdfPath();
        try
        {
            var findings = Enumerable.Range(0, 5)
                .Select(i => SampleFinding($"u-{i}", Severity.Critical))
                .ToList();

            var exception = Record.Exception(() => PdfReporter.GenerateFile(ResultWith(findings), path));

            Assert.Null(exception);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void GenerateFile_NoFindings_DoesNotThrow()
    {
        var path = TempPdfPath();
        try
        {
            var exception = Record.Exception(() => PdfReporter.GenerateFile(ResultWith([]), path));

            Assert.Null(exception);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // US-012 AC: a 200-finding report generates in under 10 seconds.
    [Fact]
    public void GenerateFile_TwoHundredFindings_CompletesInUnderTenSeconds()
    {
        var path = TempPdfPath();
        try
        {
            var findings = Enumerable.Range(0, 200)
                .Select(i => SampleFinding($"u-{i}", (Severity)(i % 5)))
                .ToList();

            var stopwatch = Stopwatch.StartNew();
            PdfReporter.GenerateFile(ResultWith(findings), path);
            stopwatch.Stop();

            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10),
                $"Expected under 10s, took {stopwatch.Elapsed.TotalSeconds:F1}s.");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void GenerateFile_WithBranding_DoesNotThrow()
    {
        var path = TempPdfPath();
        try
        {
            var branding = new ReportBranding("Acme Security Consulting", LogoPath: null);

            var exception = Record.Exception(
                () => PdfReporter.GenerateFile(ResultWith([SampleFinding("u-1", Severity.Medium)]), path, branding));

            Assert.Null(exception);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
