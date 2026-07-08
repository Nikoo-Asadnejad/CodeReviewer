using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ReviewerAgent.LlmConnector;
using ReviewerAgent.LlmConnector.Contracts;
using ReviewerAgent.SharedKernel;
using Xunit;

namespace ReviewerAgent.Tests;

public class LlmConnectorTests
{
    private static AnthropicLlmConnector Build(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var client = StubHttpMessageHandler.Client(responder, "https://api.anthropic.com/", out _);
        return new AnthropicLlmConnector(
            client,
            Options.Create(new LlmConnectorOptions { Model = "claude-opus-4-8", MaxTokens = 256 }),
            NullLogger<AnthropicLlmConnector>.Instance);
    }

    [Fact]
    public async Task Concatenates_text_content_blocks()
    {
        var connector = Build(_ => StubHttpMessageHandler.Json(
            """{"content":[{"type":"text","text":"Hello "},{"type":"text","text":"world"},{"type":"thinking","text":"ignored"}]}"""));

        var response = await connector.CompleteAsync(new LlmRequest("sys", "user"));

        Assert.Equal("Hello world", response.Text);
    }

    [Fact]
    public async Task Throws_LlmException_on_error_status()
    {
        var connector = Build(_ => StubHttpMessageHandler.Json(
            """{"error":"overloaded"}""", HttpStatusCode.InternalServerError));

        await Assert.ThrowsAsync<LlmException>(() => connector.CompleteAsync(new LlmRequest("s", "u")));
    }

    [Fact]
    public async Task Maps_timeout_to_LlmTimeoutException()
    {
        var connector = Build(_ => throw new TaskCanceledException("timed out"));

        await Assert.ThrowsAsync<LlmTimeoutException>(() => connector.CompleteAsync(new LlmRequest("s", "u")));
    }
}
