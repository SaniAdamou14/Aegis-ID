using System.Reflection;
using System.Text.Json.Serialization;
using Aegis.Controls.Iam;
using Aegis.Domain.Controls;
using Aegis.Domain.Findings;
using Aegis.Domain.Snapshot;
using Aegis.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

const string DashboardCorsPolicy = "Dashboard";
var dashboardOrigins = builder.Configuration.GetSection("Dashboard:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:4200"];

builder.Services.AddCors(options => options.AddPolicy(DashboardCorsPolicy, policy => policy
    .WithOrigins(dashboardOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(DashboardCorsPolicy);

var engine = ControlEngine.DiscoverFrom(typeof(PrivilegedAccountsWithoutStrongMfaControl).Assembly);
var historyDbPath = builder.Configuration["Persistence:DbPath"] ?? "aegis-history.db";

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }))
    .WithName("Health");

app.MapGet("/api/scan/demo", () =>
{
    var snapshot = LoadEmbeddedDemoSnapshot();
    var result = engine.Run(snapshot);
    return Results.Text(JsonReporter.Serialize(result), "application/json");
})
    .WithName("GetDemoScan")
    .WithOpenApi();

app.MapPost("/api/scan/evaluate", (TenantSnapshot snapshot) =>
{
    var result = engine.Run(snapshot);
    return Results.Text(JsonReporter.Serialize(result), "application/json");
})
    .WithName("EvaluateSnapshot")
    .WithOpenApi();

app.MapGet("/api/scans/history", async (int? limit) =>
{
    if (!File.Exists(historyDbPath))
        return Results.Ok(Array.Empty<ScanHistoryEntry>());

    using var db = AegisDbContextFactory.CreateSqlite(historyDbPath);
    var store = new ScanHistoryStore(db);
    var count = limit is > 0 and <= 100 ? limit.Value : 10;
    var records = await store.GetRecentAsync(count);

    var entries = records
        .OrderBy(r => r.EvaluatedAt)
        .Select(r => new ScanHistoryEntry(r.Id, r.EvaluatedAt, r.TenantDisplayName, r.PostureScore))
        .ToList();

    return Results.Ok(entries);
})
    .WithName("GetScanHistory")
    .WithOpenApi();

app.Run();

static TenantSnapshot LoadEmbeddedDemoSnapshot()
{
    var assembly = Assembly.GetExecutingAssembly();
    using var stream = assembly.GetManifestResourceStream("demo-tenant.json")
        ?? throw new InvalidOperationException("Embedded demo snapshot 'demo-tenant.json' not found.");
    using var reader = new StreamReader(stream);
    return TenantSnapshotSerializer.Parse(reader.ReadToEnd());
}

public partial class Program;

/// <summary>Oldest-first, for a left-to-right trend chart (US-018). Backed by the same SQLite file `aegis evaluate/scan --db` writes to.</summary>
internal sealed record ScanHistoryEntry(Guid Id, DateTime EvaluatedAt, string Tenant, int PostureScore);
