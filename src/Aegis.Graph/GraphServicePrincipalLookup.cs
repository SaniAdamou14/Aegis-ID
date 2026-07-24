using System.Text.Json;

namespace Aegis.Graph;

internal static class GraphServicePrincipalLookup
{
    public static async Task<JsonElement?> FindByAppIdAsync(
        GraphHttpClient client, string appId, CancellationToken cancellationToken)
    {
        using var page = await client.GetJsonAsync($"servicePrincipals?$filter=appId eq '{appId}'", cancellationToken);
        var values = page.RootElement.GetProperty("value");
        return values.GetArrayLength() > 0 ? values[0].Clone() : null;
    }
}

internal static class GraphAppRoleCatalog
{
    /// <summary>Maps Microsoft Graph app role IDs to their permission names (e.g. "Directory.Read.All").</summary>
    public static async Task<IReadOnlyDictionary<string, string>> LoadMicrosoftGraphRolesAsync(
        GraphHttpClient client, CancellationToken cancellationToken)
    {
        var graphSp = await GraphServicePrincipalLookup.FindByAppIdAsync(
            client, WellKnownPermissions.MicrosoftGraphAppId, cancellationToken);

        if (graphSp is null)
            return new Dictionary<string, string>();

        return graphSp.Value.GetProperty("appRoles").EnumerateArray()
            .ToDictionary(r => r.GetProperty("id").GetString()!, r => r.GetProperty("value").GetString()!);
    }
}
