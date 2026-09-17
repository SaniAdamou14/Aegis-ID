using Aegis.Domain.Findings;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Aegis.Cli.Reporting;

/// <summary>Audit-ready PDF report (US-012): cover page, executive summary with a severity chart, findings detail, methodology appendix.</summary>
public static class PdfReporter
{
    private static readonly (Severity Severity, string Hex)[] SeverityColors =
    [
        (Severity.Critical, Colors.Red.Medium),
        (Severity.High, Colors.Orange.Medium),
        (Severity.Medium, Colors.Amber.Medium),
        (Severity.Low, Colors.Grey.Medium),
        (Severity.Info, Colors.Blue.Lighten2),
    ];

    public static void GenerateFile(ScanResult result, string filePath, ReportBranding? branding = null)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10));
                page.Footer().AlignCenter().Text(t =>
                {
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });

                page.Content().Column(column =>
                {
                    column.Item().Element(c => RenderCover(c, result, branding));
                    column.Item().PageBreak();

                    column.Item().Element(c => RenderExecutiveSummary(c, result));
                    column.Item().PageBreak();

                    column.Item().Element(c => RenderFindingsDetail(c, result));
                    column.Item().PageBreak();

                    column.Item().Element(RenderMethodologyAppendix);
                });
            });
        }).GeneratePdf(filePath);
    }

    private static void RenderCover(QuestPDF.Infrastructure.IContainer container, ScanResult result, ReportBranding? branding)
    {
        container.PaddingTop(150).Column(column =>
        {
            if (branding?.LogoPath is { } logoPath && File.Exists(logoPath))
                column.Item().AlignCenter().Height(70).Image(logoPath).FitHeight();

            column.Item().PaddingTop(20).AlignCenter().Text(branding?.FirmName ?? "Aegis-ID")
                .FontSize(24).Bold();

            column.Item().AlignCenter().Text("Security Posture Audit Report").FontSize(16);

            column.Item().PaddingTop(40).AlignCenter().Text(result.TenantDisplayName).FontSize(18).SemiBold();

            column.Item().AlignCenter().Text($"Evaluated {result.EvaluatedAt:yyyy-MM-dd}").FontSize(11);

            column.Item().PaddingTop(10).AlignCenter().Text("Read-only audit - nothing on this tenant was modified.")
                .FontSize(9).Italic();
        });
    }

    private static void RenderExecutiveSummary(QuestPDF.Infrastructure.IContainer container, ScanResult result)
    {
        container.Column(column =>
        {
            column.Item().Text("Executive summary").FontSize(18).Bold();
            column.Item().PaddingTop(10).Text($"Posture score: {result.PostureScore}/100").FontSize(14);

            var passed = result.ControlResults.Count(r => r.Status == ControlStatus.Passed);
            var failed = result.ControlResults.Count(r => r.Status == ControlStatus.Failed);
            var skipped = result.ControlResults.Count(r => r.Status == ControlStatus.Skipped);
            column.Item().Text($"Controls: {passed} passed, {failed} failed, {skipped} skipped "
                + $"(of {result.ControlResults.Count} total).").FontSize(10);

            column.Item().PaddingTop(20).Text("Findings by severity").FontSize(12).SemiBold();
            column.Item().PaddingTop(8).Element(c => RenderSeverityChart(c, result));
        });
    }

    private static void RenderSeverityChart(QuestPDF.Infrastructure.IContainer container, ScanResult result)
    {
        var max = Math.Max(1, SeverityColors.Max(s => result.CountBySeverity(s.Severity)));

        container.Column(column =>
        {
            foreach (var (severity, hex) in SeverityColors)
            {
                var count = result.CountBySeverity(severity);
                var weight = Math.Clamp((float)count / max, 0.02f, 0.98f);

                column.Item().PaddingBottom(4).Row(row =>
                {
                    row.ConstantItem(60).Text(severity.ToString());
                    row.RelativeItem().Height(14).Row(barRow =>
                    {
                        barRow.RelativeItem(weight).Background(hex);
                        barRow.RelativeItem(1 - weight);
                    });
                    row.ConstantItem(30).AlignRight().Text(count.ToString());
                });
            }
        });
    }

    private static void RenderFindingsDetail(QuestPDF.Infrastructure.IContainer container, ScanResult result)
    {
        container.Column(column =>
        {
            column.Item().Text("Findings detail").FontSize(18).Bold();

            column.Item().PaddingTop(10).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(50);
                    columns.ConstantColumn(55);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(3);
                });

                table.Header(header =>
                {
                    header.Cell().Text("Severity").SemiBold();
                    header.Cell().Text("Control").SemiBold();
                    header.Cell().Text("Object").SemiBold();
                    header.Cell().Text("Evidence").SemiBold();
                    header.Cell().Text("Remediation").SemiBold();
                });

                foreach (var finding in result.AllFindings.OrderByDescending(f => f.Severity))
                {
                    table.Cell().Text(finding.Severity.ToString());
                    table.Cell().Text(finding.ControlId);
                    table.Cell().Text($"{finding.ObjectType}: {finding.ObjectName}");
                    table.Cell().Text(finding.Evidence);
                    table.Cell().Text(string.Join(" ", finding.Remediation.Select((step, i) => $"{i + 1}. {step}")));
                }
            });
        });
    }

    private static void RenderMethodologyAppendix(QuestPDF.Infrastructure.IContainer container)
    {
        container.Column(column =>
        {
            column.Item().Text("Methodology").FontSize(18).Bold();

            column.Item().PaddingTop(10).Text(
                "Aegis-ID audits a Microsoft Entra ID tenant read-only: it never requests a Microsoft Graph "
                + "write scope, and modifies, deletes, or creates nothing. Each control evaluates a snapshot "
                + "of the tenant's identity configuration (users, roles, authentication methods, applications, "
                + "Conditional Access policies) against a declarative rule, and produces a finding when the "
                + "configuration does not meet that rule.");

            column.Item().PaddingTop(10).Text(
                "Findings are classified Critical, High, Medium, Low, or Info. The posture score starts at 100 "
                + "and is reduced by a fixed penalty per finding severity (Critical 15, High 8, Medium 4, Low 1, "
                + "Info 0), floored at 0. A finding marked as an expected exception (e.g. a documented "
                + "break-glass account) or as suppressed does not count against the score.");

            column.Item().PaddingTop(10).Text(
                "Each control cites, where applicable, a CIS Microsoft 365 Foundations Benchmark section and a "
                + "MITRE ATT&CK technique. See docs/controls/IAM-XXX.md in the Aegis-ID repository for each "
                + "control's exact logic and known false positives, and docs/threat-model.md for what this audit "
                + "does and does not cover.");
        });
    }
}
