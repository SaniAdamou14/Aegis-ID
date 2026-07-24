using System.Reflection;
using Aegis.Cli.Reporting;
using Aegis.Controls.Iam;
using Aegis.Domain.Controls;
using Aegis.Domain.Findings;
using Aegis.Domain.Snapshot;

var engine = ControlEngine.DiscoverFrom(typeof(PrivilegedAccountsWithoutStrongMfaControl).Assembly);

if (args.Length == 0)
{
    PrintUsage();
    return 2;
}

return args[0] switch
{
    "demo" => RunDemo(),
    "evaluate" => RunEvaluate(args.Skip(1).ToArray()),
    _ => Unknown(args[0]),
};

int RunDemo()
{
    var snapshot = LoadEmbeddedDemoSnapshot();
    var result = engine.Run(snapshot);
    ConsoleReporter.Report(result, Console.Out);
    return 0;
}

int RunEvaluate(string[] rest)
{
    string? fromPath = null;
    Severity? failOn = null;

    for (var i = 0; i < rest.Length; i++)
    {
        switch (rest[i])
        {
            case "--from" when i + 1 < rest.Length:
                fromPath = rest[++i];
                break;

            case "--fail-on" when i + 1 < rest.Length:
                if (!Enum.TryParse<Severity>(rest[i + 1], ignoreCase: true, out var parsed))
                {
                    Console.Error.WriteLine(
                        $"Invalid --fail-on value '{rest[i + 1]}'. Expected one of: Critical, High, Medium, Low, Info.");
                    return 2;
                }
                failOn = parsed;
                i++;
                break;

            default:
                Console.Error.WriteLine($"Unknown argument '{rest[i]}'.");
                return 2;
        }
    }

    if (fromPath is null)
    {
        Console.Error.WriteLine("Missing required argument --from <snapshot.json>.");
        return 2;
    }

    TenantSnapshot snapshot;
    try
    {
        snapshot = TenantSnapshotSerializer.Load(fromPath);
    }
    catch (Exception ex) when (ex is IOException or InvalidDataException or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine($"Failed to load snapshot '{fromPath}': {ex.Message}");
        return 2;
    }

    var result = engine.Run(snapshot);
    ConsoleReporter.Report(result, Console.Out);

    if (failOn is { } threshold)
    {
        var hasBreach = result.AllFindings.Any(f => !f.IsExpectedException && f.Severity >= threshold);
        return hasBreach ? 1 : 0;
    }

    return 0;
}

TenantSnapshot LoadEmbeddedDemoSnapshot()
{
    var assembly = Assembly.GetExecutingAssembly();
    using var stream = assembly.GetManifestResourceStream("demo-tenant.json")
        ?? throw new InvalidOperationException("Embedded demo snapshot 'demo-tenant.json' not found.");
    using var reader = new StreamReader(stream);
    return TenantSnapshotSerializer.Parse(reader.ReadToEnd());
}

int Unknown(string command)
{
    Console.Error.WriteLine($"Unknown command '{command}'.");
    PrintUsage();
    return 2;
}

void PrintUsage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  aegis demo");
    Console.WriteLine("  aegis evaluate --from <snapshot.json> [--fail-on <Severity>]");
}
