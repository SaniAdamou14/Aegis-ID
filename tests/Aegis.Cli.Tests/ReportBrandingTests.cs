using Aegis.Cli.Reporting;

namespace Aegis.Cli.Tests;

public class ReportBrandingTests
{
    private static string WriteTempFile(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"aegis-branding-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }

    [Fact]
    public void Load_ValidFile_ReturnsFirmNameAndLogoPath()
    {
        var path = WriteTempFile("""{ "firmName": "Acme Security Consulting", "logoPath": "logo.png" }""");

        try
        {
            var branding = ReportBranding.Load(path);

            Assert.Equal("Acme Security Consulting", branding.FirmName);
            Assert.Equal("logo.png", branding.LogoPath);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_FirmNameOnly_LeavesLogoPathNull()
    {
        var path = WriteTempFile("""{ "firmName": "Acme Security Consulting" }""");

        try
        {
            var branding = ReportBranding.Load(path);

            Assert.Equal("Acme Security Consulting", branding.FirmName);
            Assert.Null(branding.LogoPath);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
