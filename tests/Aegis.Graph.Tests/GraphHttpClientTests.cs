using System.Net;
using System.Net.Http.Headers;

namespace Aegis.Graph.Tests;

public class GraphHttpClientTests
{
    private const string Url = "https://graph.microsoft.com/v1.0/users";

    [Fact]
    public async Task GetJsonAsync_RetriesOn429ThenSucceeds()
    {
        var throttled = HttpResponseFactory.Json(HttpStatusCode.TooManyRequests, "{}");
        throttled.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMilliseconds(1));

        var handler = new QueueHandler(new Queue<HttpResponseMessage>(
        [
            throttled,
            HttpResponseFactory.Json(HttpStatusCode.OK, """{ "value": [] }"""),
        ]));

        using var httpClient = new HttpClient(handler);
        var delay = new RecordingRetryDelay();
        using var client = new GraphHttpClient(httpClient, _ => Task.FromResult("token"), delay);

        using var result = await client.GetJsonAsync(Url);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Single(delay.Delays);
    }

    [Fact]
    public async Task GetJsonAsync_RetriesOn500UpToMaxAttemptsThenThrows()
    {
        var handler = new QueueHandler(new Queue<HttpResponseMessage>(
            Enumerable.Range(0, GraphHttpClient.MaxAttempts)
                .Select(_ => HttpResponseFactory.Json(HttpStatusCode.InternalServerError, "{}"))));

        using var httpClient = new HttpClient(handler);
        var delay = new RecordingRetryDelay();
        using var client = new GraphHttpClient(httpClient, _ => Task.FromResult("token"), delay);

        var ex = await Assert.ThrowsAsync<GraphRequestException>(() => client.GetJsonAsync(Url));

        Assert.Equal(GraphHttpClient.MaxAttempts, handler.Requests.Count);
        Assert.Equal(HttpStatusCode.InternalServerError, ex.StatusCode);
        Assert.Equal(GraphHttpClient.MaxAttempts - 1, delay.Delays.Count);
    }

    [Fact]
    public async Task GetJsonAsync_NonRetryableError_ThrowsImmediately()
    {
        var handler = new QueueHandler(new Queue<HttpResponseMessage>(
        [
            HttpResponseFactory.Json(HttpStatusCode.Forbidden, "{}"),
        ]));

        using var httpClient = new HttpClient(handler);
        using var client = new GraphHttpClient(httpClient, _ => Task.FromResult("token"), new RecordingRetryDelay());

        await Assert.ThrowsAsync<GraphRequestException>(() => client.GetJsonAsync(Url));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task GetAllPagesAsync_FollowsODataNextLink()
    {
        var page1 = HttpResponseFactory.Json(HttpStatusCode.OK,
            """{ "value": [{"id":"1"}], "@odata.nextLink": "https://graph.microsoft.com/v1.0/users?page=2" }""");
        var page2 = HttpResponseFactory.Json(HttpStatusCode.OK, """{ "value": [{"id":"2"}] }""");

        var handler = new QueueHandler(new Queue<HttpResponseMessage>([page1, page2]));
        using var httpClient = new HttpClient(handler);
        using var client = new GraphHttpClient(httpClient, _ => Task.FromResult("token"), new RecordingRetryDelay());

        var ids = new List<string>();
        await foreach (var item in client.GetAllPagesAsync(Url))
            ids.Add(item.GetProperty("id").GetString()!);

        Assert.Equal(["1", "2"], ids);
        Assert.Equal(2, handler.Requests.Count);
    }
}
