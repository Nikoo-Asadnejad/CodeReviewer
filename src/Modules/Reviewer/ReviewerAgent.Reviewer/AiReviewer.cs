using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using ReviewerAgent.ContextBuilder.Contracts;
using ReviewerAgent.LlmConnector.Contracts;
using ReviewerAgent.PromptManager.Contracts;
using ReviewerAgent.Reviewer.Contracts;

namespace ReviewerAgent.Reviewer;

/// <summary>
/// Sends the prompt to the configured LLM (via the connector) and parses the structured
/// JSON response into a <see cref="ReviewResult"/>.
/// </summary>
internal sealed class AiReviewer(ILlmConnector llm, ILogger<AiReviewer> logger) : IReviewer
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: true) },
    };

    private readonly ILlmConnector _llm = llm;
    private readonly ILogger<AiReviewer> _logger = logger;

    public async Task<ReviewResult> ReviewAsync(
        ReviewPrompt prompt,
        ReviewContext context,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var response = await _llm.CompleteAsync(new LlmRequest(prompt.System, prompt.User), cancellationToken);
        sw.Stop();
        _logger.LogInformation("LLM responded in {Ms}ms ({Files} file(s) in context).",
            sw.ElapsedMilliseconds, context.Code.Files.Count);

        var json = StripCodeFences(response.Text);
        if (string.IsNullOrWhiteSpace(json))
        {
            _logger.LogWarning("LLM returned an empty response; no findings.");
            return ReviewResult.Empty;
        }

        try
        {
            var result = JsonSerializer.Deserialize<ReviewResult>(json, Json);
            if (result is null)
            {
                return ReviewResult.Empty;
            }

            _logger.LogInformation("Parsed {Count} finding(s) from LLM response.", result.Findings.Count);
            return result;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse LLM response as JSON; returning no findings.");
            return ReviewResult.Empty;
        }
    }

    /// <summary>Removes a surrounding Markdown code fence (``` or ```json) if present.</summary>
    internal static string StripCodeFences(string text)
    {
        var trimmed = text.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var firstNewline = trimmed.IndexOf('\n');
        if (firstNewline < 0)
        {
            return trimmed;
        }

        var body = trimmed[(firstNewline + 1)..];
        var lastFence = body.LastIndexOf("```", StringComparison.Ordinal);
        return (lastFence >= 0 ? body[..lastFence] : body).Trim();
    }
}
