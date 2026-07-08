namespace ReviewerAgent.TaskConnector.Contracts;

/// <summary>
/// Connects to a task-management host to fetch work items referenced by a pull request.
/// </summary>
public interface ITaskConnector
{
    /// <summary>The provider key this connector handles (e.g. "azuredevops").</summary>
    string Provider { get; }

    /// <summary>Fetches a work item by id, or null if it does not exist.</summary>
    Task<WorkItem?> GetWorkItemAsync(string id, CancellationToken cancellationToken = default);
}

/// <summary>Resolves the <see cref="ITaskConnector"/> matching the configured provider.</summary>
public interface ITaskConnectorFactory
{
    ITaskConnector Create();
}
