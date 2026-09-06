using System.Net;

namespace GitHubRefollow.Tests.TestSupport;

internal sealed record RequestSnapshot(HttpMethod Method, Uri? RequestUri, string? Authorization);

internal sealed class StubHttpMessageHandler(
    Func<HttpRequestMessage, int, HttpResponseMessage> responseFactory) : HttpMessageHandler
{
    private int requestIndex;

    public List<RequestSnapshot> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Requests.Add(new RequestSnapshot(
            request.Method,
            request.RequestUri,
            request.Headers.Authorization?.ToString()));

        return Task.FromResult(responseFactory(request, requestIndex++));
    }

    public static HttpResponseMessage Json(
        string json,
        HttpStatusCode statusCode = HttpStatusCode.OK,
        string? link = null)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };

        if (link is not null)
        {
            response.Headers.TryAddWithoutValidation("Link", link);
        }

        return response;
    }
}
