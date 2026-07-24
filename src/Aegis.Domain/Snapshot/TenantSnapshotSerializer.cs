using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aegis.Domain.Snapshot;

public static class TenantSnapshotSerializer
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static TenantSnapshot Load(string path)
    {
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<TenantSnapshot>(stream, Options)
            ?? throw new InvalidDataException($"Snapshot file '{path}' deserialized to null.");
    }

    public static TenantSnapshot Parse(string json) =>
        JsonSerializer.Deserialize<TenantSnapshot>(json, Options)
            ?? throw new InvalidDataException("Snapshot JSON deserialized to null.");

    public static string Serialize(TenantSnapshot snapshot) =>
        JsonSerializer.Serialize(snapshot, Options);
}
