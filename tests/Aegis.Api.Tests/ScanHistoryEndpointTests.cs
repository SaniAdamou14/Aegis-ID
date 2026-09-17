using System.Net.Http.Json;
using Aegis.Domain.Findings;
using Aegis.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Aegis.Api.Tests;

public class ScanHistoryEndpointTests
{
    private sealed record ScanHistoryEntry(Guid Id, DateTime EvaluatedAt, string Tenant, int PostureScore);

    [Fact]
    public async Task NoHistoryDatabase_ReturnsEmptyArray()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("Persistence:DbPath", Path.Combine(Path.GetTempPath(), $"aegis-missing-{Guid.NewGuid():N}.db")));
        var client = factory.CreateClient();

        var entries = await client.GetFromJsonAsync<List<ScanHistoryEntry>>("/api/scans/history");

        Assert.NotNull(entries);
        Assert.Empty(entries);
    }

    [Fact]
    public async Task WithRecordedScans_ReturnsThemOldestFirst()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"aegis-history-{Guid.NewGuid():N}.db");

        try
        {
            using (var db = AegisDbContextFactory.CreateSqlite(dbPath))
            {
                var store = new ScanHistoryStore(db);
                await store.SaveAsync(SampleResult("Older", 60, DateTimeOffset.UtcNow.AddHours(-2)));
                await store.SaveAsync(SampleResult("Newer", 90, DateTimeOffset.UtcNow));
            }

            await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
                builder.UseSetting("Persistence:DbPath", dbPath));
            var client = factory.CreateClient();

            var entries = await client.GetFromJsonAsync<List<ScanHistoryEntry>>("/api/scans/history");

            Assert.NotNull(entries);
            Assert.Equal(2, entries!.Count);
            Assert.Equal("Older", entries[0].Tenant);
            Assert.Equal("Newer", entries[1].Tenant);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try { File.Delete(dbPath); } catch (IOException) { /* pooled connection may still be closing; temp file, not critical */ }
        }
    }

    private static ScanResult SampleResult(string tenant, int score, DateTimeOffset evaluatedAt) => new(
        tenant, evaluatedAt, [new ControlResult("IAM-003", "Title", ControlStatus.Passed, [])], score);
}
