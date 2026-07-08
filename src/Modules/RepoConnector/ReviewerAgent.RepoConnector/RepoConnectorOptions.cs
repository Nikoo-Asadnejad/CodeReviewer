namespace ReviewerAgent.RepoConnector;

/// <summary>Repository-host connection settings. Bound from the "RepoConnector" section.</summary>
public sealed class RepoConnectorOptions
{
    public const string SectionName = "RepoConnector";

    /// <summary>Provider key: "azuredevops", "github", or "gitlab".</summary>
    public string Provider { get; set; } = "azuredevops";

    /// <summary>
    /// Host API root. Defaults to Azure DevOps; use "https://api.github.com" for GitHub
    /// or "https://gitlab.com/api/v4" (or a self-hosted equivalent) for GitLab.
    /// </summary>
    public string BaseUrl { get; set; } = "https://dev.azure.com";

    /// <summary>Azure DevOps organization. For GitHub this is the repository owner (user/org).</summary>
    public string Organization { get; set; } = string.Empty;

    public string Project { get; set; } = string.Empty;

    /// <summary>
    /// Azure DevOps / GitHub repository name. For GitLab this is the project: either its numeric
    /// id or its URL-encoded path (e.g. "group/subgroup/project").
    /// </summary>
    public string Repository { get; set; } = string.Empty;

    /// <summary>
    /// Access token. Azure DevOps PAT (Basic auth), GitHub token (Bearer), or GitLab token
    /// (PRIVATE-TOKEN). Overridable via AZURE_DEVOPS_PAT / GITHUB_TOKEN / GITLAB_TOKEN.
    /// </summary>
    public string Pat { get; set; } = string.Empty;
}
