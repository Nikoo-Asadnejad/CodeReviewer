using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ReviewerAgent.LlmConnector;
using ReviewerAgent.LlmConnector.Contracts;
using ReviewerAgent.SharedKernel;
using Xunit;

namespace ReviewerAgent.Tests;

public class OllamaLlmConnectorTests
{
    private static OllamaLlmConnector Build(Func<HttpRequestMessage, HttpResponseMessage> responder, out StubHttpMessageHandler handler)
    {
        var client = StubHttpMessageHandler.Client(responder, "http://localhost:11434/", out handler);
        return new OllamaLlmConnector(
            client,
            Options.Create(new LlmConnectorOptions { Provider = "ollama", Model = "llama3", MaxTokens = 256 }),
            NullLogger<OllamaLlmConnector>.Instance);
    }

    [Fact]
    public async Task Returns_message_content()
    {
        var connector = Build(_ => StubHttpMessageHandler.Json(
            """{"model":"llama3","message":{"role":"assistant","content":"Hello world"},"done":true}"""), out _);

        var response = await connector.CompleteAsync(new LlmRequest("sys", "user"));

        Assert.Equal("Hello world", response.Text);
    }

    [Fact]
    public async Task Posts_chat_request_with_streaming_disabled_and_system_message()
    {
        string? body = null;
        string? path = null;
        var connector = Build(req =>
        {
            path = req.RequestUri!.AbsolutePath;
            body = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return StubHttpMessageHandler.Json("""{"message":{"role":"assistant","content":"ok"}}""");
        }, out _);

        await connector.CompleteAsync(new LlmRequest("be terse", "review this"));

        Assert.Equal("/api/chat", path);
        Assert.NotNull(body);
        Assert.Contains("\"model\":\"llama3\"", body);
        Assert.Contains("\"stream\":false", body);
        Assert.Contains("\"num_predict\":256", body);
        Assert.Contains("be terse", body);
        Assert.Contains("review this", body);
    }

    [Fact]
    public async Task Omits_system_message_when_blank()
    {
        string? body = null;
        var connector = Build(req =>
        {
            body = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return StubHttpMessageHandler.Json("""{"message":{"role":"assistant","content":"ok"}}""");
        }, out _);

        await connector.CompleteAsync(new LlmRequest("", "review this"));

        Assert.NotNull(body);
        Assert.DoesNotContain("system", body);
    }

    [Fact]
    public async Task Throws_LlmException_on_error_status()
    {
        var connector = Build(_ => StubHttpMessageHandler.Json(
            """{"error":"model not found"}""", HttpStatusCode.NotFound), out _);

        await Assert.ThrowsAsync<LlmException>(() => connector.CompleteAsync(new LlmRequest("s", "u")));
    }

    [Fact]
    public async Task Maps_timeout_to_LlmTimeoutException()
    {
        var connector = Build(_ => throw new TaskCanceledException("timed out"), out _);

        await Assert.ThrowsAsync<LlmTimeoutException>(() => connector.CompleteAsync(new LlmRequest("s", "u")));
    }
}
