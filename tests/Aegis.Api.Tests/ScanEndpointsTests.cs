using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Aegis.Api.Tests;

public class ScanEndpointsTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Health_ReturnsOk()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetDemoScan_ReturnsReportWithAllControls()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/scan/demo");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        Assert.Equal("1.0", root.GetProperty("schemaVersion").GetString());
        Assert.Equal(15, root.GetProperty("controls").GetArrayLength());
    }

    [Fact]
    public async Task PostEvaluate_WithMinimalSnapshot_ReturnsPassedControls()
    {
        var client = factory.CreateClient();
        var snapshot = new
        {
            tenantInfo = new { tenantId = "t-1", displayName = "Test Tenant" },
            users = Array.Empty<object>(),
            applications = Array.Empty<object>(),
            conditionalAccessPolicies = Array.Empty<object>(),
            collectedAt = "2026-01-01T00:00:00Z",
        };

        var response = await client.PostAsJsonAsync("/api/scan/evaluate", snapshot);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Test Tenant", doc.RootElement.GetProperty("tenant").GetString());
    }
}
