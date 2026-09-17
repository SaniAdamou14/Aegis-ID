using Aegis.Domain.Findings;
using Aegis.Domain.Snapshot;
using Aegis.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Aegis.Persistence.Tests;

public class ScanHistoryStoreTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly AegisDbContext _db;
    private readonly ScanHistoryStore _store;

    public ScanHistoryStoreTests()
    {
        _connection.Open();
        var options = new DbContextOptionsBuilder<AegisDbContext>().UseSqlite(_connection).Options;
        _db = new AegisDbContext(options);
        _db.Database.EnsureCreated();
        _store = new ScanHistoryStore(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private static ScanResult SampleResult(string tenant = "Fixture Tenant", int score = 90, DateTimeOffset? evaluatedAt = null) => new(
        tenant,
        evaluatedAt ?? DateTimeOffset.UtcNow,
        [new ControlResult("IAM-003", "Title", ControlStatus.Passed, [])],
        score);

    [Fact]
    public async Task SaveAsync_ThenGetById_RoundTripsTheRecord()
    {
        var saved = await _store.SaveAsync(SampleResult());

        var fetched = await _store.GetByIdAsync(saved.Id);

        Assert.NotNull(fetched);
        Assert.Equal("Fixture Tenant", fetched!.TenantDisplayName);
        Assert.Equal(90, fetched.PostureScore);
        Assert.Contains("\"schemaVersion\"", fetched.ReportJson);
    }

    [Fact]
    public async Task GetLatestAsync_ReturnsTheMostRecentlyEvaluatedScan()
    {
        await _store.SaveAsync(SampleResult(evaluatedAt: DateTimeOffset.UtcNow.AddHours(-2)));
        var newest = await _store.SaveAsync(SampleResult(score: 70, evaluatedAt: DateTimeOffset.UtcNow));

        var latest = await _store.GetLatestAsync();

        Assert.Equal(newest.Id, latest!.Id);
    }

    [Fact]
    public async Task SaveAsync_DeletesRecordsOlderThanRetention()
    {
        await _store.SaveAsync(SampleResult(evaluatedAt: DateTimeOffset.UtcNow.AddDays(-100)));

        await _store.SaveAsync(SampleResult(evaluatedAt: DateTimeOffset.UtcNow), retention: TimeSpan.FromDays(90));

        var remaining = await _store.GetRecentAsync(10);
        Assert.Single(remaining);
    }

    [Fact]
    public async Task GetRecentAsync_ReturnsNewestFirstUpToCount()
    {
        for (var i = 0; i < 5; i++)
            await _store.SaveAsync(SampleResult(evaluatedAt: DateTimeOffset.UtcNow.AddMinutes(-i)));

        var recent = await _store.GetRecentAsync(3);

        Assert.Equal(3, recent.Count);
        Assert.True(recent[0].EvaluatedAt > recent[1].EvaluatedAt);
    }
}
