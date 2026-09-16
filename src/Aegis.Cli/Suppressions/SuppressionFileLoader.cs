using Aegis.Domain.Findings;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Aegis.Cli.Suppressions;

/// <summary>Loads and validates an `aegis-suppressions.yaml` file (US-008).</summary>
public static class SuppressionFileLoader
{
    public static IReadOnlyList<Suppression> Load(string path)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var yaml = File.ReadAllText(path);
        var entries = deserializer.Deserialize<List<SuppressionDto>>(yaml) ?? [];

        var suppressions = new List<Suppression>();
        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.ControlId))
                throw new InvalidDataException($"Suppression entry in '{path}' is missing required field 'controlId'.");

            if (string.IsNullOrWhiteSpace(entry.ObjectId))
                throw new InvalidDataException($"Suppression entry in '{path}' is missing required field 'objectId'.");

            if (string.IsNullOrWhiteSpace(entry.Reason))
                throw new InvalidDataException(
                    $"Suppression for {entry.ControlId}/{entry.ObjectId} in '{path}' is missing a required 'reason'.");

            suppressions.Add(new Suppression(entry.ControlId, entry.ObjectId, entry.Reason, entry.Expires));
        }

        return suppressions;
    }

    private sealed class SuppressionDto
    {
        public string? ControlId { get; set; }
        public string? ObjectId { get; set; }
        public string? Reason { get; set; }
        public DateTimeOffset? Expires { get; set; }
    }
}
