using System.Net;

namespace SoundTouchMCP.Tests.TestDoubles;

internal record CapturedHttpRequest(string Method, string Url, string? Body);

internal sealed class TestHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, string?, CancellationToken, Task<HttpResponseMessage>> _responder;
    private readonly object _requestsLock = new();

    public TestHttpMessageHandler(Func<HttpRequestMessage, string?, HttpResponseMessage> responder)
    {
        _responder = (request, body, _) => Task.FromResult(responder(request, body));
    }

    public TestHttpMessageHandler(Func<HttpRequestMessage, string?, CancellationToken, Task<HttpResponseMessage>> responder)
    {
        _responder = responder;
    }

    public List<CapturedHttpRequest> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string? body = null;
        if (request.Content is not null)
            body = await request.Content.ReadAsStringAsync(cancellationToken);

        lock (_requestsLock)
        {
            Requests.Add(new CapturedHttpRequest(
                request.Method.Method,
                request.RequestUri?.ToString() ?? string.Empty,
                body));
        }

        var response = await _responder(request, body, cancellationToken);
        response.RequestMessage = request;
        return response;
    }

    public static HttpResponseMessage XmlResponse(HttpStatusCode statusCode, string xml)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(xml)
        };
    }
}
