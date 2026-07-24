using Azure.Core;
using Azure.Identity;

namespace Aegis.Graph;

/// <summary>Client credentials flow (app-only, no user interaction) — US-001.</summary>
public sealed class GraphAuthenticator
{
    private static readonly string[] Scopes = ["https://graph.microsoft.com/.default"];
    private readonly ClientSecretCredential _credential;

    public GraphAuthenticator(string tenantId, string clientId, string clientSecret) =>
        _credential = new ClientSecretCredential(tenantId, clientId, clientSecret);

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
