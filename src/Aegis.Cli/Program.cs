using System.Reflection;
using Aegis.Cli.Reporting;
using Aegis.Cli.Suppressions;
using Aegis.Controls.Iam;
using Aegis.Domain.Controls;
using Aegis.Domain.Findings;
using Aegis.Domain.Snapshot;
using Aegis.Graph;

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
    "doctor" => await RunDoctorAsync(args.Skip(1).ToArray()),
    "scan" => await RunScanAsync(args.Skip(1).ToArray()),
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
    string outputFormat = "console";
    string? filePath = null;
    string? suppressionsPath = null;
    var quiet = false;
    var verbose = false;

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

            case "--output" when i + 1 < rest.Length:
                outputFormat = rest[++i].ToLowerInvariant();
                break;

            case "--file" when i + 1 < rest.Length:
                filePath = rest[++i];
                break;

            case "--suppressions" when i + 1 < rest.Length:
                suppressionsPath = rest[++i];
                break;

            case "--quiet":
                quiet = true;
                break;

            case "--verbose":
                verbose = true;
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

    if (!TryApplySuppressions(suppressionsPath, ref result))
        return 2;

    var writeExitCode = WriteReport(result, outputFormat, filePath, quiet, verbose);
    if (writeExitCode != 0)
        return writeExitCode;

    if (failOn is { } threshold)
    {
        var hasBreach = result.AllFindings.Any(f => !f.IsExpectedException && !f.IsSuppressed && f.Severity >= threshold);
        return hasBreach ? 1 : 0;
    }

    return 0;
}

bool TryApplySuppressions(string? suppressionsPath, ref ScanResult result)
{
    if (suppressionsPath is null)
        return true;

    IReadOnlyList<Suppression> suppressions;
    try
    {
        suppressions = SuppressionFileLoader.Load(suppressionsPath);
    }
    catch (Exception ex) when (ex is IOException or InvalidDataException)
    {
        Console.Error.WriteLine($"Failed to load suppressions '{suppressionsPath}': {ex.Message}");
        return false;
    }

    var applied = SuppressionApplier.Apply(result, suppressions, DateTimeOffset.UtcNow);
    result = applied.ScanResult;

    if (applied.ExpiredSuppressions.Count > 0)
    {
        Console.WriteLine("WARNING: the following suppressions have expired and no longer apply:");
        foreach (var expired in applied.ExpiredSuppressions)
            Console.WriteLine($"  {expired.ControlId}/{expired.ObjectId}: expired {expired.Expires:yyyy-MM-dd} ({expired.Reason})");
        Console.WriteLine();
    }

    return true;
}

int WriteReport(ScanResult result, string outputFormat, string? filePath, bool quiet, bool verbose)
{
    switch (outputFormat)
    {
        case "console":
            ConsoleReporter.Report(result, Console.Out, quiet, verbose);
            return 0;

        case "json":
            if (filePath is null)
            {
                Console.Error.WriteLine("--output json requires --file <path>.");
                return 2;
            }
            File.WriteAllText(filePath, JsonReporter.Serialize(result));
            return 0;

        case "csv":
            if (filePath is null)
            {
                Console.Error.WriteLine("--output csv requires --file <path>.");
                return 2;
            }
            File.WriteAllText(filePath, CsvReporter.Serialize(result));
            return 0;

        default:
            Console.Error.WriteLine($"Unknown --output value '{outputFormat}'. Expected one of: console, json, csv.");
            return 2;
    }
}

async Task<int> RunDoctorAsync(string[] rest)
{
    if (!TryParseCredentialArgs(rest, out var tenantId, out var clientId, out var secret, out var remaining, out var credentialError))
    {
        Console.Error.WriteLine(credentialError);
        return 2;
    }

    if (remaining.Count > 0)
    {
        Console.Error.WriteLine($"Unknown argument '{remaining[0]}'.");
        return 2;
    }

    using var client = GraphHttpClient.Create(tenantId, clientId, secret);
    DoctorReport report;

    try
    {
        report = await new PermissionChecker(client).CheckAsync(clientId);
    }
    catch (GraphAuthenticationException ex)
    {
        Console.Error.WriteLine($"Authentication failed: {ex.Message}");
        return 2;
    }
    catch (GraphRequestException ex)
    {
        Console.Error.WriteLine($"Graph request failed: {ex.Message}");
        return 2;
    }

    Console.WriteLine("Required permissions:");
    foreach (var permission in report.RequiredPermissions)
    {
        var status = permission.Status == PermissionStatus.Granted ? "granted" : "missing";
        Console.WriteLine($"  [{status}] {permission.Permission}");
    }

    if (report.UnexpectedWriteScopes.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine("WARNING: the following write scopes are granted but not needed by Aegis-ID:");
        foreach (var scope in report.UnexpectedWriteScopes)
            Console.WriteLine($"  {scope} — recommend removing this permission from the app registration.");
    }

    if (!report.AllRequiredGranted)
    {
        Console.WriteLine();
        Console.WriteLine("Some required permissions are missing; affected controls will be skipped during a scan.");
        return 1;
    }

    return 0;
}

async Task<int> RunScanAsync(string[] rest)
{
    if (!TryParseCredentialArgs(rest, out var tenantId, out var clientId, out var secret, out var remaining, out var credentialError))
    {
        Console.Error.WriteLine(credentialError);
        return 2;
    }

    Severity? failOn = null;
    string? dumpPath = null;
    string outputFormat = "console";
    string? filePath = null;
    string? suppressionsPath = null;
    var quiet = false;
    var verbose = false;

    for (var i = 0; i < remaining.Count; i++)
    {
        switch (remaining[i])
        {
            case "--fail-on" when i + 1 < remaining.Count:
                if (!Enum.TryParse<Severity>(remaining[i + 1], ignoreCase: true, out var parsed))
                {
                    Console.Error.WriteLine(
                        $"Invalid --fail-on value '{remaining[i + 1]}'. Expected one of: Critical, High, Medium, Low, Info.");
                    return 2;
                }
                failOn = parsed;
                i++;
                break;

            case "--dump" when i + 1 < remaining.Count:
                dumpPath = remaining[++i];
                break;

            case "--output" when i + 1 < remaining.Count:
                outputFormat = remaining[++i].ToLowerInvariant();
                break;

            case "--file" when i + 1 < remaining.Count:
                filePath = remaining[++i];
                break;

            case "--suppressions" when i + 1 < remaining.Count:
                suppressionsPath = remaining[++i];
                break;

            case "--quiet":
                quiet = true;
                break;

            case "--verbose":
                verbose = true;
                break;

            default:
                Console.Error.WriteLine($"Unknown argument '{remaining[i]}'.");
                return 2;
        }
    }

    using var client = GraphHttpClient.Create(
        tenantId, clientId, secret,
        verboseLogger: verbose ? line => Console.WriteLine($"[graph] {line}") : null);
    TenantSnapshot snapshot;

    try
    {
        snapshot = await new GraphTenantCollector(client).CollectAsync();
    }
    catch (GraphAuthenticationException ex)
    {
        Console.Error.WriteLine($"Authentication failed: {ex.Message}");
        return 2;
    }
    catch (GraphRequestException ex)
    {
        Console.Error.WriteLine($"Graph request failed: {ex.Message}");
        return 2;
    }

    if (dumpPath is not null)
        File.WriteAllText(dumpPath, TenantSnapshotSerializer.Serialize(snapshot));

    var result = engine.Run(snapshot);

    if (!TryApplySuppressions(suppressionsPath, ref result))
        return 2;

    var writeExitCode = WriteReport(result, outputFormat, filePath, quiet, verbose);
    if (writeExitCode != 0)
        return writeExitCode;

    if (failOn is { } threshold)
    {
        var hasBreach = result.AllFindings.Any(f => !f.IsExpectedException && !f.IsSuppressed && f.Severity >= threshold);
        return hasBreach ? 1 : 0;
    }

    return 0;
}

bool TryParseCredentialArgs(
    string[] rest,
    out string tenantId,
    out string clientId,
    out string secret,
    out List<string> remaining,
    out string? error)
{
    string? parsedTenantId = null;
    string? parsedClientId = null;
    string? parsedSecret = null;
    remaining = [];

    for (var i = 0; i < rest.Length; i++)
    {
        switch (rest[i])
        {
            case "--tenant-id" when i + 1 < rest.Length:
                parsedTenantId = rest[++i];
                break;
            case "--client-id" when i + 1 < rest.Length:
                parsedClientId = rest[++i];
                break;
            case "--secret" when i + 1 < rest.Length:
                parsedSecret = rest[++i];
                break;
            default:
                remaining.Add(rest[i]);
                break;
        }
    }

    parsedSecret ??= Environment.GetEnvironmentVariable("AEGIS_CLIENT_SECRET");

    if (parsedTenantId is null)
    {
        error = "Missing required argument --tenant-id.";
    }
    else if (parsedClientId is null)
    {
        error = "Missing required argument --client-id.";
    }
    else if (parsedSecret is null)
    {
        error = "Missing client secret: pass --secret or set the AEGIS_CLIENT_SECRET environment variable.";
    }
    else
    {
        error = null;
    }

    tenantId = parsedTenantId!;
    clientId = parsedClientId!;
    secret = parsedSecret!;
    return error is null;
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
    Console.WriteLine("                 [--output console|json|csv] [--file <path>]");
    Console.WriteLine("                 [--suppressions <suppressions.yaml>] [--quiet] [--verbose]");
    Console.WriteLine("  aegis doctor --tenant-id <id> --client-id <id> [--secret <secret>]");
    Console.WriteLine("  aegis scan --tenant-id <id> --client-id <id> [--secret <secret>]");
    Console.WriteLine("             [--fail-on <Severity>] [--dump <snapshot.json>]");
    Console.WriteLine("             [--output console|json|csv] [--file <path>]");
    Console.WriteLine("             [--suppressions <suppressions.yaml>] [--quiet] [--verbose]");
    Console.WriteLine();
    Console.WriteLine("  The client secret can also be provided via the AEGIS_CLIENT_SECRET environment variable.");
    Console.WriteLine("  --output defaults to console. --output json and --output csv write the report to --file <path> instead of stdout.");
    Console.WriteLine("  --suppressions loads documented finding exceptions from a YAML file (see docs/suppressions.md).");
    Console.WriteLine("  --quiet reduces console output to the score and severity counts. --verbose adds per-control durations");
    Console.WriteLine("  (and, for scan, each Graph HTTP call).");
}
