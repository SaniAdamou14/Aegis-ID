using System.Reflection;
using System.Text.Json.Serialization;
using Aegis.Controls.Iam;
using Aegis.Domain.Controls;
using Aegis.Domain.Findings;
using Aegis.Domain.Snapshot;

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
