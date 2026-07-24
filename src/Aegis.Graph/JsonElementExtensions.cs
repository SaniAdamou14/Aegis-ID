using System.Globalization;
using System.Text.Json;

namespace Aegis.Graph;

internal static class JsonElementExtensions
{
    public static string? GetStringOrNull(this JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetString()
            : null;

    public static DateTimeOffset? GetDateTimeOffsetOrNull(this JsonElement element, string propertyName)
    {
        var raw = element.GetStringOrNull(propertyName);
        return raw is null ? null : DateTimeOffset.Parse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }

    public static IReadOnlyList<string> GetStringArray(this JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
            return [];

        return value.EnumerateArray()
            .Select(e => e.GetString())
            .Where(s => s is not null)
            .Select(s => s!)
            .ToList();
    }
}
