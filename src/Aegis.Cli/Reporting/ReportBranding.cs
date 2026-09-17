using System.Text.Json;

namespace Aegis.Cli.Reporting;

/// <summary>Firm name/logo for the PDF cover page (US-012), loaded from a small JSON config file.</summary>
public sealed record ReportBranding(string? FirmName, string? LogoPath)
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static ReportBranding Load(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<ReportBranding>(json, Options)
            ?? throw new InvalidDataException($"Branding file '{path}' deserialized to null.");
    }
}
