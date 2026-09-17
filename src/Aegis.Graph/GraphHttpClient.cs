using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Aegis.Graph;

/// <summary>
/// Thin, resilient Microsoft Graph HTTP client: retries on 429/5xx (respecting
/// Retry-After, up to 5 attempts, exponential backoff with jitter otherwise)
/// and transparently follows @odata.nextLink pagination (US-004).
/// </summary>
public sealed class GraphHttpClient : IDisposable
{
    public const int MaxAttempts = 5;

    private readonly HttpClient _httpClient;
    private readonly Func<CancellationToken, Task<string>> _tokenProvider;
    private readonly IRetryDelay _retryDelay;
    private readonly bool _ownsHttpClient;
    private readonly Action<string>? _verboseLogger;

    public GraphHttpClient(
        HttpClient httpClient,
        Func<CancellationToken, Task<string>> tokenProvider,
        IRetryDelay? retryDelay = null,
        bool ownsHttpClient = false,
        Action<string>? verboseLogger = null)
    {
        _httpClient = httpClient;
        _tokenProvider = tokenProvider;
        _retryDelay = retryDelay ?? new SystemRetryDelay();
        _ownsHttpClient = ownsHttpClient;
        _verboseLogger = verboseLogger;
    }

    public async Task<JsonDocument> GetJsonAsync(string relativeOrAbsoluteUrl, CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, relativeOrAbsoluteUrl);
            var token = await _tokenProvider(cancellationToken);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var stopwatch = Stopwatch.StartNew();
            var response = await _httpClient.SendAsync(request, cancellationToken);
            stopwatch.Stop();

            if (response.IsSuccessStatusCode)
            {
                _verboseLogger?.Invoke($"GET {relativeOrAbsoluteUrl} -> {(int)response.StatusCode} ({stopwatch.ElapsedMilliseconds}ms)");
                var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            }

            var isRetryable = response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500;
            if (!isRetryable || attempt >= MaxAttempts)
            {
                _verboseLogger?.Invoke(
                    $"GET {relativeOrAbsoluteUrl} -> {(int)response.StatusCode} ({stopwatch.ElapsedMilliseconds}ms) — giving up");
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new GraphRequestException(response.StatusCode, relativeOrAbsoluteUrl, body);
            }

            _verboseLogger?.Invoke(
                $"GET {relativeOrAbsoluteUrl} -> {(int)response.StatusCode} ({stopwatch.ElapsedMilliseconds}ms) — retrying (attempt {attempt}/{MaxAttempts})");
            var delay = ComputeDelay(response.Headers.RetryAfter, attempt);
            await _retryDelay.DelayAsync(delay, cancellationToken);
        }
    }

    internal static TimeSpan ComputeDelay(RetryConditionHeaderValue? retryAfter, int attempt)
    {
        if (retryAfter?.Delta is { } delta)
            return delta;

        if (retryAfter?.Date is { } date)
        {
            var wait = date - DateTimeOffset.UtcNow;
            if (wait > TimeSpan.Zero)
                return wait;
        }

        var exponential = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
        var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 500));
        return exponential + jitter;
    }

    public async IAsyncEnumerable<JsonElement> GetAllPagesAsync(
        string relativeUrl,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        string? next = relativeUrl;

        while (next is not null)
        {
            using var page = await GetJsonAsync(next, cancellationToken);
            var root = page.RootElement;

            if (root.TryGetProperty("value", out var values))
            {
                foreach (var item in values.EnumerateArray())
                    yield return item.Clone();
            }

            next = root.TryGetProperty("@odata.nextLink", out var nextLink) ? nextLink.GetString() : null;
        }
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
            _httpClient.Dispose();
    }

    /// <summary>Builds a client wired for https://graph.microsoft.com/v1.0 using client credentials auth.</summary>
    public static GraphHttpClient Create(string tenantId, string clientId, string clientSecret, Action<string>? verboseLogger = null)
    {
        var authenticator = new GraphAuthenticator(tenantId, clientId, clientSecret);
        var httpClient = new HttpClient { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") };
        return new GraphHttpClient(httpClient, authenticator.GetAccessTokenAsync, ownsHttpClient: true, verboseLogger: verboseLogger);
    }

    /// <summary>Builds a client wired for https://graph.microsoft.com/v1.0 using device code auth (US-002) — no client secret, delegated permissions.</summary>
    public static GraphHttpClient CreateInteractive(
        string tenantId, string clientId, Action<string> onDeviceCode, Action<string>? verboseLogger = null)
    {
        var authenticator = GraphAuthenticator.CreateInteractive(tenantId, clientId, onDeviceCode);
        var httpClient = new HttpClient { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") };
        return new GraphHttpClient(httpClient, authenticator.GetAccessTokenAsync, ownsHttpClient: true, verboseLogger: verboseLogger);
    }
}
