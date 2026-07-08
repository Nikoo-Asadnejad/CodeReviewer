using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ReviewerAgent.LlmConnector.Contracts;

namespace ReviewerAgent.LlmConnector;

/// <summary>Registers the LlmConnector module.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddLlmConnectorModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Options pattern with fail-fast validation: misconfiguration throws a clear
        // OptionsValidationException at host startup instead of surfacing deep inside the
        // HttpClient factory on first use.
        services.AddOptions<LlmConnectorOptions>()
            .Bind(configuration.GetSection(LlmConnectorOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl), "LlmConnector:BaseUrl must be set.")
            .Validate(o => o.MaxTokens > 0, "LlmConnector:MaxTokens must be greater than zero.")
            .Validate(o => o.TimeoutSeconds > 0, "LlmConnector:TimeoutSeconds must be greater than zero.")
            .Validate(
                o => !string.Equals(o.Provider, "anthropic", StringComparison.OrdinalIgnoreCase)
                     || !string.IsNullOrWhiteSpace(o.ApiKey)
                     || !string.IsNullOrWhiteSpace(o.AuthToken),
                "No LLM credential configured. Set LlmConnector:ApiKey (env LlmConnector__ApiKey or CLAUDE_API_KEY) " +
                "for an Anthropic API key, or LlmConnector:AuthToken (env LlmConnector__AuthToken or ANTHROPIC_AUTH_TOKEN) " +
                "for a Claude subscription OAuth token.")
            .ValidateOnStart();

        services.AddHttpClient<AnthropicLlmConnector>(ConfigureClient);
        services.AddHttpClient<OllamaLlmConnector>(ConfigureOllamaClient);

        services.AddSingleton<ILlmConnectorFactory, LlmConnectorFactory>();
        services.AddTransient<ILlmConnector>(sp => sp.GetRequiredService<ILlmConnectorFactory>().Create());
        return services;
    }

    private static void ConfigureOllamaClient(IServiceProvider sp, HttpClient client)
    {
        var o = sp.GetRequiredService<IOptions<LlmConnectorOptions>>().Value;
        client.BaseAddress = new Uri($"{o.BaseUrl.TrimEnd('/')}/");
        client.Timeout = TimeSpan.FromSeconds(o.TimeoutSeconds);

        // Ollama needs no credential by default; a token is only used when a secured proxy
        // sits in front of it.
        var token = !string.IsNullOrWhiteSpace(o.ApiKey) ? o.ApiKey : o.AuthToken;
        if (!string.IsNullOrWhiteSpace(token))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    private static void ConfigureClient(IServiceProvider sp, HttpClient client)
    {
        var o = sp.GetRequiredService<IOptions<LlmConnectorOptions>>().Value;
        client.BaseAddress = new Uri($"{o.BaseUrl.TrimEnd('/')}/");
        client.Timeout = TimeSpan.FromSeconds(o.TimeoutSeconds);
        client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

        // Auth precedence: an API key (x-api-key) wins when present; otherwise fall back
        // to a Claude subscription OAuth access token (Authorization: Bearer + the OAuth
        // beta header).
        if (!string.IsNullOrWhiteSpace(o.ApiKey))
        {
            client.DefaultRequestHeaders.Add("x-api-key", o.ApiKey);
        }
        else if (!string.IsNullOrWhiteSpace(o.AuthToken))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", o.AuthToken);
            client.DefaultRequestHeaders.Add("anthropic-beta", "oauth-2025-04-20");
        }
        else
        {
            throw new InvalidOperationException(
                "No LLM credential configured. Set LlmConnector:ApiKey (or the CLAUDE_API_KEY env var) " +
                "for an Anthropic API key, or LlmConnector:AuthToken (or the ANTHROPIC_AUTH_TOKEN env var) " +
                "for a Claude subscription OAuth token.");
        }
    }
}
