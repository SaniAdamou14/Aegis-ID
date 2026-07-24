using System.Net;
using System.Text;

namespace Aegis.Graph.Tests;

internal sealed class QueueHandler(Queue<HttpResponseMessage> responses) : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(responses.Dequeue());
    }
}

/// <summary>Routes each request to a canned JSON body by exact absolute URL.</summary>
internal sealed class RoutedHandler(IReadOnlyDictionary<string, string> responsesByUrl) : HttpMessageHandler
{
    public List<string> RequestedUrls { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri!.ToString();
        RequestedUrls.Add(url);

        if (!responsesByUrl.TryGetValue(url, out var json))
            throw new InvalidOperationException($"No fixture mapped for request URL: {url}");

        return Task.FromResult(HttpResponseFactory.Json(HttpStatusCode.OK, json));
    }
}

internal sealed class RecordingRetryDelay : IRetryDelay
{
    public List<TimeSpan> Delays { get; } = [];

    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        Delays.Add(delay);
        return Task.CompletedTask;
    }
}

internal static class HttpResponseFactory
{
    public static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}
