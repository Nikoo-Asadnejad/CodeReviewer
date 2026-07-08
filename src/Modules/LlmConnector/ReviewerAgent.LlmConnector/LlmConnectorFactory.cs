using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ReviewerAgent.LlmConnector.Contracts;
using ReviewerAgent.SharedKernel;

namespace ReviewerAgent.LlmConnector;

/// <summary>Selects the LLM connector implementation matching the configured provider.</summary>
internal sealed class LlmConnectorFactory(IServiceProvider services, IOptions<LlmConnectorOptions> options) : ILlmConnectorFactory
{
    private readonly IServiceProvider _services = services;
    private readonly LlmConnectorOptions _options = options.Value;

    public ILlmConnector Create() => _options.Provider.ToLowerInvariant() switch
    {
        "anthropic" => _services.GetRequiredService<AnthropicLlmConnector>(),
        "ollama" => _services.GetRequiredService<OllamaLlmConnector>(),
        _ => throw new LlmException($"Unsupported LLM provider '{_options.Provider}'."),
    };
}
