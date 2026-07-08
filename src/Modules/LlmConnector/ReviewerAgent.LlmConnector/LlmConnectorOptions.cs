namespace ReviewerAgent.LlmConnector;

/// <summary>LLM provider settings. Bound from the "LlmConnector" section.</summary>
public sealed class LlmConnectorOptions
{
    public const string SectionName = "LlmConnector";

    /// <summary>Provider key: "anthropic" or "ollama".</summary>
    public string Provider { get; set; } = "anthropic";

    /// <summary>
    /// Model id. For Anthropic, e.g. "claude-opus-4-8"; for Ollama, a locally pulled tag
    /// such as "llama3" or "qwen2.5-coder".
    /// </summary>
    public string Model { get; set; } = "claude-opus-4-8";

    /// <summary>
    /// Anthropic API key (x-api-key auth). Takes priority over <see cref="AuthToken"/>.
    /// Overridable via the CLAUDE_API_KEY / LLM_API_KEY env vars.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// OAuth access token for a Claude subscription (Pro/Max). Used only when
    /// <see cref="ApiKey"/> is empty, via Authorization: Bearer + the OAuth beta header.
    /// Overridable via the ANTHROPIC_AUTH_TOKEN / CLAUDE_CODE_OAUTH_TOKEN env vars.
    /// </summary>
    public string AuthToken { get; set; } = string.Empty;

    /// <summary>
    /// API root. Defaults to the Anthropic API; set to a local Ollama host
    /// (e.g. "http://localhost:11434") when <see cref="Provider"/> is "ollama".
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.anthropic.com";

    public int MaxTokens { get; set; } = 4096;

    public int TimeoutSeconds { get; set; } = 120;
}
