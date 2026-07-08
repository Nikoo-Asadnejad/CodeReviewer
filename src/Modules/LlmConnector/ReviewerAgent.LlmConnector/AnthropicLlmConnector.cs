using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReviewerAgent.LlmConnector.Contracts;
using ReviewerAgent.SharedKernel;

namespace ReviewerAgent.LlmConnector;

/// <summary>
/// Anthropic Messages API implementation of <see cref="ILlmConnector"/>. The HttpClient is
/// configured (base address, x-api-key, anthropic-version, timeout) by the DI layer.
/// Sampling parameters are intentionally omitted — current Claude models reject them.
/// </summary>
internal sealed class AnthropicLlmConnector(HttpClient http, IOptions<LlmConnectorOptions> options, ILogger<AnthropicLlmConnector> logger) : ILlmConnector
{
    private static readonly JsonSerializerOptions RequestJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly JsonSerializerOptions ResponseJson = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http = http;
    private readonly LlmConnectorOptions _options = options.Value;
    private readonly ILogger<AnthropicLlmConnector> _logger = logger;

    public string Provider => "anthropic";

    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        var payload = new AnthropicRequest(
            Model: _options.Model,
            MaxTokens: _options.MaxTokens,
            System: request.System,
            Messages: [new AnthropicMessage("user", request.User)]);

        _logger.LogInformation("Sending prompt: {Chars} chars to anthropic/{Model}", request.User.Length, _options.Model);

        HttpResponseMessage response;
        try
        {
            using var body = JsonContent.Create(payload, options: RequestJson);
            response = await _http.PostAsync("v1/messages", body, cancellationToken);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new LlmTimeoutException("The LLM request timed out.", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var detail = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new LlmException($"Anthropic returned {(int)response.StatusCode}: {detail}");
            }

            var parsed = await response.Content.ReadFromJsonAsync<AnthropicResponse>(ResponseJson, cancellationToken);
            var text = string.Concat(
                (parsed?.Content ?? [])
                .Where(b => string.Equals(b.Type, "text", StringComparison.OrdinalIgnoreCase))
                .Select(b => b.Text));
            return new LlmResponse(text);
        }
    }

    private sealed record AnthropicRequest(string Model, int MaxTokens, string System, IReadOnlyList<AnthropicMessage> Messages);

    private sealed record AnthropicMessage(string Role, string Content);

    private sealed record AnthropicResponse(IReadOnlyList<AnthropicContentBlock>? Content);

    private sealed record AnthropicContentBlock(string? Type, string? Text);
}
