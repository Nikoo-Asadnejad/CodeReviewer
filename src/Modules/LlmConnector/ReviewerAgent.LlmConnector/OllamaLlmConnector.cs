using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReviewerAgent.LlmConnector.Contracts;
using ReviewerAgent.SharedKernel;

namespace ReviewerAgent.LlmConnector;

/// <summary>
/// Ollama implementation of <see cref="ILlmConnector"/> over the local <c>/api/chat</c> endpoint.
/// The HttpClient (base address, optional auth, timeout) is configured by the DI layer. Streaming
/// is disabled so the full completion arrives in a single response.
/// </summary>
internal sealed class OllamaLlmConnector(HttpClient http, IOptions<LlmConnectorOptions> options, ILogger<OllamaLlmConnector> logger) : ILlmConnector
{
    private static readonly JsonSerializerOptions RequestJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly JsonSerializerOptions ResponseJson = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http = http;
    private readonly LlmConnectorOptions _options = options.Value;
    private readonly ILogger<OllamaLlmConnector> _logger = logger;

    public string Provider => "ollama";

    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        var messages = new List<OllamaMessage>(2);
        if (!string.IsNullOrWhiteSpace(request.System))
        {
            messages.Add(new OllamaMessage("system", request.System));
        }
        messages.Add(new OllamaMessage("user", request.User));

        var payload = new OllamaRequest(
            Model: _options.Model,
            Messages: messages,
            Stream: false,
            Options: new OllamaParameters(NumPredict: _options.MaxTokens));

        _logger.LogInformation("Sending prompt: {Chars} chars to ollama/{Model}", request.User.Length, _options.Model);

        HttpResponseMessage response;
        try
        {
            using var body = JsonContent.Create(payload, options: RequestJson);
            response = await _http.PostAsync("api/chat", body, cancellationToken);
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
                throw new LlmException($"Ollama returned {(int)response.StatusCode}: {detail}");
            }

            var parsed = await response.Content.ReadFromJsonAsync<OllamaResponse>(ResponseJson, cancellationToken);
            return new LlmResponse(parsed?.Message?.Content ?? string.Empty);
        }
    }

    private sealed record OllamaRequest(string Model, IReadOnlyList<OllamaMessage> Messages, bool Stream, OllamaParameters Options);

    private sealed record OllamaMessage(string Role, string Content);

    // Ollama caps the completion length via options.num_predict.
    private sealed record OllamaParameters(int NumPredict);

    private sealed record OllamaResponse(OllamaMessage? Message);
}
