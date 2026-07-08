using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ReviewerAgent.SharedKernel;
using ReviewerAgent.TaskConnector.Contracts;

namespace ReviewerAgent.TaskConnector;

/// <summary>Selects the task connector implementation matching the configured provider.</summary>
internal sealed class TaskConnectorFactory(IServiceProvider services, IOptions<TaskConnectorOptions> options) : ITaskConnectorFactory
{
    private readonly IServiceProvider _services = services;
    private readonly TaskConnectorOptions _options = options.Value;

    public ITaskConnector Create() => _options.Provider.ToLowerInvariant() switch
    {
        "azuredevops" => _services.GetRequiredService<AzureDevOpsTaskConnector>(),
        "clickup" => _services.GetRequiredService<ClickUpTaskConnector>(),
        "jira" => _services.GetRequiredService<JiraTaskConnector>(),
        _ => throw new RepoConnectorException($"Unsupported task provider '{_options.Provider}'."),
    };
}
