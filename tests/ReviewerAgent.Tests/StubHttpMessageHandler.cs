using System.Net;
using System.Text;

namespace ReviewerAgent.Tests;

/// <summary>
/// Test double for <see cref="HttpClient"/> that routes each request through a supplied
/// function, records the requests it saw, and surfaces thrown exceptions as faulted tasks
/// (so timeout/cancellation paths can be exercised).
/// </summary>
internal sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        try
        {
            return Task.FromResult(responder(request));
        }
        catch (Exception ex)
        {
            return Task.FromException<HttpResponseMessage>(ex);
        }
    }

    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    public static HttpClient Client(Func<HttpRequestMessage, HttpResponseMessage> responder, string baseUrl, out StubHttpMessageHandler handler)
    {
        handler = new StubHttpMessageHandler(responder);
        return new HttpClient(handler) { BaseAddress = new Uri(baseUrl) };
    }
}
