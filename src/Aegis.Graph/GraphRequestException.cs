using System.Net;

namespace Aegis.Graph;

public sealed class GraphRequestException(HttpStatusCode statusCode, string requestUrl, string responseBody)
    : Exception($"Graph request to '{requestUrl}' failed with {(int)statusCode} {statusCode}.")
{
    public HttpStatusCode StatusCode { get; } = statusCode;
    public string RequestUrl { get; } = requestUrl;
    public string ResponseBody { get; } = responseBody;
}
