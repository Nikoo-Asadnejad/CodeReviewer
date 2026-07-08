namespace ReviewerAgent.LlmConnector.Contracts;

/// <summary>Sends prompts to an LLM provider and returns the raw text completion.</summary>
public interface ILlmConnector
{
    /// <summary>The provider key this connector handles (e.g. "anthropic", "openai").</summary>
    string Provider { get; }

    /// <summary>Submits the request and returns the completion.</summary>
    Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Resolves the <see cref="ILlmConnector"/> matching the configured provider.</summary>
public interface ILlmConnectorFactory
{
    ILlmConnector Create();
}
