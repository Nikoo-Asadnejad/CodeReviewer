using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ReviewerAgent.RepoConnector.Contracts;
using ReviewerAgent.SharedKernel;

namespace ReviewerAgent.RepoConnector;

/// <summary>Selects the repo connector implementation matching the configured provider.</summary>
internal sealed class RepoConnectorFactory(IServiceProvider services, IOptions<RepoConnectorOptions> options) : IRepoConnectorFactory
{
    private readonly IServiceProvider _services = services;
    private readonly RepoConnectorOptions _options = options.Value;

    public IRepoConnector Create() => _options.Provider.ToLowerInvariant() switch
    {
        "azuredevops" => _services.GetRequiredService<AzureDevOpsRepoConnector>(),
        "github" => _services.GetRequiredService<GitHubRepoConnector>(),
        "gitlab" => _services.GetRequiredService<GitLabRepoConnector>(),
        _ => throw new RepoConnectorException($"Unsupported repo provider '{_options.Provider}'."),
    };
}
