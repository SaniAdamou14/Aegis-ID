using System.Reflection;
using Aegis.Cli.Reporting;
using Aegis.Cli.Suppressions;
using Aegis.Controls.Iam;
using Aegis.Domain.Controls;
using Aegis.Domain.Findings;
using Aegis.Domain.Snapshot;
using Aegis.Graph;
using Aegis.Persistence;

var engine = ControlEngine.DiscoverFrom(typeof(PrivilegedAccountsWithoutStrongMfaControl).Assembly);

if (args.Length == 0)
{
    PrintUsage();
    return 2;
}

return args[0] switch
{
    "demo" => RunDemo(),
    "evaluate" => await RunEvaluateAsync(args.Skip(1).ToArray()),
    "doctor" => await RunDoctorAsync(args.Skip(1).ToArray()),
    "scan" => await RunScanAsync(args.Skip(1).ToArray()),
    "diff" => await RunDiffAsync(args.Skip(1).ToArray()),
    _ => Unknown(args[0]),
};

int RunDemo()
{
    var snapshot = LoadEmbeddedDemoSnapshot();
    var result = engine.Run(snapshot);
    ConsoleReporter.Report(result, Console.Out);
    return 0;
}

async Task<int> RunEvaluateAsync(string[] rest)
{
    string? fromPath = null;
    Severity? failOn = null;
    string outputFormat = "console";
    string? filePath = null;
    string? suppressionsPath = null;
    string? dbPath = null;
    string? brandingPath = null;
    var retentionDays = 90;
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

            case "--branding" when i + 1 < rest.Length:
                brandingPath = rest[++i];
                break;

            case "--db" when i + 1 < rest.Length:
                dbPath = rest[++i];
                break;

            case "--retention-days" when i + 1 < rest.Length:
                if (!int.TryParse(rest[i + 1], out retentionDays) || retentionDays < 1)
                {
                    Console.Error.WriteLine($"Invalid --retention-days value '{rest[i + 1]}'. Expected a positive integer.");
                    return 2;
                }
                i++;
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

    if (dbPath is not null)
        await SaveToHistoryAsync(dbPath, retentionDays, result);

    var writeExitCode = WriteReport(result, outputFormat, filePath, quiet, verbose, brandingPath);
    if (writeExitCode != 0)
        return writeExitCode;

    if (failOn is { } threshold)
    {
        var hasBreach = result.AllFindings.Any(f => !f.IsExpectedException && !f.IsSuppressed && f.Severity >= threshold);
        return hasBreach ? 1 : 0;
    }

    return 0;
}

async Task SaveToHistoryAsync(string dbPath, int retentionDays, ScanResult result)
{
    using var db = AegisDbContextFactory.CreateSqlite(dbPath);
    var store = new ScanHistoryStore(db);
    var record = await store.SaveAsync(result, TimeSpan.FromDays(retentionDays));
    Console.WriteLine($"Saved scan {record.Id} to '{dbPath}' (retention: {retentionDays} days).");
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

int WriteReport(ScanResult result, string outputFormat, string? filePath, bool quiet, bool verbose, string? brandingPath = null)
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

        case "pdf":
            if (filePath is null)
            {
                Console.Error.WriteLine("--output pdf requires --file <path>.");
                return 2;
            }

            ReportBranding? branding = null;
            if (brandingPath is not null)
            {
                try
                {
                    branding = ReportBranding.Load(brandingPath);
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or System.Text.Json.JsonException)
                {
                    Console.Error.WriteLine($"Failed to load branding '{brandingPath}': {ex.Message}");
                    return 2;
                }
            }

            PdfReporter.GenerateFile(result, filePath, branding);
            return 0;

        default:
            Console.Error.WriteLine($"Unknown --output value '{outputFormat}'. Expected one of: console, json, csv, pdf.");
            return 2;
    }
}

async Task<int> RunDoctorAsync(string[] rest)
{
    if (!TryParseCredentialArgs(rest, requireSecret: true, out var tenantId, out var clientId, out var secret, out var remaining, out var credentialError))
    {
        Console.Error.WriteLine(credentialError);
        return 2;
    }

    if (remaining.Count > 0)
    {
        Console.Error.WriteLine($"Unknown argument '{remaining[0]}'.");
        return 2;
    }

    using var client = GraphHttpClient.Create(tenantId, clientId, secret!);
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
            Console.WriteLine($"  {scope} - recommend removing this permission from the app registration.");
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
    var interactive = rest.Contains("--interactive");

    if (!TryParseCredentialArgs(rest, requireSecret: !interactive, out var tenantId, out var clientId, out var secret, out var remaining, out var credentialError))
    {
        Console.Error.WriteLine(credentialError);
        return 2;
    }

    Severity? failOn = null;
    string? dumpPath = null;
    string outputFormat = "console";
    string? filePath = null;
    string? suppressionsPath = null;
    string? dbPath = null;
    string? brandingPath = null;
    var retentionDays = 90;
    var quiet = false;
    var verbose = false;

    for (var i = 0; i < remaining.Count; i++)
    {
        switch (remaining[i])
        {
            case "--interactive":
                break; // already resolved above; consumed here so it isn't flagged as unknown

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

            case "--branding" when i + 1 < remaining.Count:
                brandingPath = remaining[++i];
                break;

            case "--db" when i + 1 < remaining.Count:
                dbPath = remaining[++i];
                break;

            case "--retention-days" when i + 1 < remaining.Count:
                if (!int.TryParse(remaining[i + 1], out retentionDays) || retentionDays < 1)
                {
                    Console.Error.WriteLine($"Invalid --retention-days value '{remaining[i + 1]}'. Expected a positive integer.");
                    return 2;
                }
                i++;
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

    var verboseLogger = verbose ? (Action<string>)(line => Console.WriteLine($"[graph] {line}")) : null;
    using var client = interactive
        ? GraphHttpClient.CreateInteractive(tenantId, clientId, onDeviceCode: Console.WriteLine, verboseLogger)
        : GraphHttpClient.Create(tenantId, clientId, secret!, verboseLogger);

    // A device code expires 15 minutes after it's issued (Entra ID's own default) — this timeout
    // makes that explicit rather than relying on however Azure.Identity happens to word the failure.
    using var interactiveTimeout = interactive ? new CancellationTokenSource(TimeSpan.FromMinutes(15)) : null;
    var cancellationToken = interactiveTimeout?.Token ?? CancellationToken.None;

    TenantSnapshot snapshot;

    try
    {
        snapshot = await new GraphTenantCollector(client).CollectAsync(cancellationToken);
    }
    catch (OperationCanceledException) when (interactive)
    {
        Console.Error.WriteLine("Device code sign-in timed out after 15 minutes without confirmation.");
        return 2;
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

    if (dbPath is not null)
        await SaveToHistoryAsync(dbPath, retentionDays, result);

    var writeExitCode = WriteReport(result, outputFormat, filePath, quiet, verbose, brandingPath);
    if (writeExitCode != 0)
        return writeExitCode;

    if (failOn is { } threshold)
    {
        var hasBreach = result.AllFindings.Any(f => !f.IsExpectedException && !f.IsSuppressed && f.Severity >= threshold);
        return hasBreach ? 1 : 0;
    }

    return 0;
}

async Task<int> RunDiffAsync(string[] rest)
{
    string? dbPath = null;
    Guid? against = null;

    for (var i = 0; i < rest.Length; i++)
    {
        switch (rest[i])
        {
            case "--db" when i + 1 < rest.Length:
                dbPath = rest[++i];
                break;

            case "--against" when i + 1 < rest.Length:
                if (!Guid.TryParse(rest[i + 1], out var parsedId))
                {
                    Console.Error.WriteLine($"Invalid --against value '{rest[i + 1]}'. Expected a scan id (GUID).");
                    return 2;
                }
                against = parsedId;
                i++;
                break;

            default:
                Console.Error.WriteLine($"Unknown argument '{rest[i]}'.");
                return 2;
        }
    }

    if (dbPath is null)
    {
        Console.Error.WriteLine("Missing required argument --db <path>.");
        return 2;
    }

    if (against is null)
    {
        Console.Error.WriteLine("Missing required argument --against <scanId>.");
        return 2;
    }

    if (!File.Exists(dbPath))
    {
        Console.Error.WriteLine($"No history database found at '{dbPath}'.");
        return 2;
    }

    using var db = AegisDbContextFactory.CreateSqlite(dbPath);
    var store = new ScanHistoryStore(db);

    var current = await store.GetLatestAsync();
    if (current is null)
    {
        Console.Error.WriteLine($"No scans recorded in '{dbPath}' yet.");
        return 2;
    }

    var baseline = await store.GetByIdAsync(against.Value);
    if (baseline is null)
    {
        Console.Error.WriteLine($"No scan found with id '{against}' in '{dbPath}'.");
        return 2;
    }

    if (current.Id == baseline.Id)
    {
        Console.Error.WriteLine("--against refers to the latest recorded scan; nothing to compare.");
        return 2;
    }

    var currentFindings = ScanRecordJsonReader.ReadFindings(current.ReportJson);
    var baselineFindings = ScanRecordJsonReader.ReadFindings(baseline.ReportJson);
    var diff = ScanDiffCalculator.Compare(currentFindings, baselineFindings);

    DiffReporter.Report(diff, current, baseline, Console.Out);

    var hasBreach = diff.Appeared.Any(f => f.Severity is Severity.Critical or Severity.High);
    return hasBreach ? 1 : 0;
}

bool TryParseCredentialArgs(
    string[] rest,
    bool requireSecret,
    out string tenantId,
    out string clientId,
    out string? secret,
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
    else if (requireSecret && parsedSecret is null)
    {
        error = "Missing client secret: pass --secret or set the AEGIS_CLIENT_SECRET environment variable.";
    }
    else
    {
        error = null;
    }

    tenantId = parsedTenantId!;
    clientId = parsedClientId!;
    secret = parsedSecret;
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
    Console.WriteLine("                 [--output console|json|csv|pdf] [--file <path>] [--branding <branding.json>]");
    Console.WriteLine("                 [--suppressions <suppressions.yaml>] [--quiet] [--verbose]");
    Console.WriteLine("                 [--db <history.db>] [--retention-days <n>]");
    Console.WriteLine("  aegis doctor --tenant-id <id> --client-id <id> [--secret <secret>]");
    Console.WriteLine("  aegis scan --tenant-id <id> --client-id <id> [--secret <secret>] | --interactive");
    Console.WriteLine("             [--fail-on <Severity>] [--dump <snapshot.json>]");
    Console.WriteLine("             [--output console|json|csv|pdf] [--file <path>] [--branding <branding.json>]");
    Console.WriteLine("             [--suppressions <suppressions.yaml>] [--quiet] [--verbose]");
    Console.WriteLine("             [--db <history.db>] [--retention-days <n>]");
    Console.WriteLine("  aegis diff --db <history.db> --against <scanId>");
    Console.WriteLine();
    Console.WriteLine("  The client secret can also be provided via the AEGIS_CLIENT_SECRET environment variable.");
    Console.WriteLine("  --interactive signs in via device code instead of a client secret: aegis prints a code and a URL,");
    Console.WriteLine("  waits for you to confirm in a browser, and evaluates with your account's own delegated permissions.");
    Console.WriteLine("  Gives up after 15 minutes without confirmation.");
    Console.WriteLine("  --output defaults to console. --output json, csv, and pdf write the report to --file <path> instead of stdout.");
    Console.WriteLine("  --output pdf produces a client-ready audit report (cover page, executive summary, findings detail,");
    Console.WriteLine("  methodology). --branding points to a JSON file ({\"firmName\": \"...\", \"logoPath\": \"...\"}) to put your");
    Console.WriteLine("  own name and logo on the cover page instead of Aegis-ID's.");
    Console.WriteLine("  --suppressions loads documented finding exceptions from a YAML file (see docs/suppressions.md).");
    Console.WriteLine("  --quiet reduces console output to the score and severity counts. --verbose adds per-control durations");
    Console.WriteLine("  (and, for scan, each Graph HTTP call).");
    Console.WriteLine("  --db records the scan in a local SQLite history file (retention: 90 days by default); omit it and nothing");
    Console.WriteLine("  is persisted. aegis diff compares the latest recorded scan in --db against the --against scan id and");
    Console.WriteLine("  exits 1 if a new Critical or High finding appeared.");
}
