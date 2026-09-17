using Azure.Core;
using Azure.Identity;

namespace Aegis.Graph;

/// <summary>Wraps a Graph token credential — client credentials (app-only, US-001) or device code (delegated, US-002).</summary>
public sealed class GraphAuthenticator
{
    private static readonly string[] Scopes = ["https://graph.microsoft.com/.default"];
    private readonly TokenCredential _credential;

    public GraphAuthenticator(string tenantId, string clientId, string clientSecret) =>
        _credential = new ClientSecretCredential(tenantId, clientId, clientSecret);

    private GraphAuthenticator(TokenCredential credential) => _credential = credential;

    /// <summary>Device code flow (US-002): no client secret, delegated permissions, a human confirms the code shown via <paramref name="onDeviceCode"/> at the printed URL.</summary>
    public static GraphAuthenticator CreateInteractive(string tenantId, string clientId, Action<string> onDeviceCode)
    {
        var options = new DeviceCodeCredentialOptions
        {
            TenantId = tenantId,
            ClientId = clientId,
            DeviceCodeCallback = (info, _) =>
            {
                onDeviceCode(info.Message);
                return Task.CompletedTask;
            },
        };

        return new GraphAuthenticator(new DeviceCodeCredential(options));
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var token = await _credential.GetTokenAsync(new TokenRequestContext(Scopes), cancellationToken);
            return token.Token;
        }
        catch (AuthenticationFailedException ex)
        {
            var detail = ex.InnerException?.Message;
            var message = string.IsNullOrWhiteSpace(detail) ? ex.Message : $"{ex.Message} {detail}";
            throw new GraphAuthenticationException(message, ex);
        }
    }
}

public sealed class GraphAuthenticationException(string message, Exception innerException)
    : Exception(message, innerException);
