namespace ReviewerAgent.TaskConnector;

/// <summary>Task-host connection settings. Bound from the "TaskConnector" section.</summary>
public sealed class TaskConnectorOptions
{
    public const string SectionName = "TaskConnector";

    /// <summary>Provider key, e.g. "azuredevops".</summary>
    public string Provider { get; set; } = "azuredevops";

    public string BaseUrl { get; set; } = "https://dev.azure.com";

    public string Organization { get; set; } = string.Empty;

    public string Project { get; set; } = string.Empty;

    /// <summary>Personal Access Token. Overridable via the AZURE_DEVOPS_PAT env var.</summary>
    public string Pat { get; set; } = string.Empty;
}
