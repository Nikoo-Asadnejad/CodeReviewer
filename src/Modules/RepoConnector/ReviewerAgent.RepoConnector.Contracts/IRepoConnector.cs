namespace ReviewerAgent.RepoConnector.Contracts;

/// <summary>
/// Connects to a source-control host (Azure DevOps, GitLab, ...) to read pull requests
/// and changed files and to publish review comments.
/// </summary>
public interface IRepoConnector
{
    /// <summary>The provider key this connector handles (e.g. "azuredevops").</summary>
    string Provider { get; }

    /// <summary>Fetches metadata for the pull request.</summary>
    Task<PullRequest> GetPullRequestAsync(int prId, CancellationToken cancellationToken = default);

    /// <summary>Fetches the files changed in the pull request, with diffs and content.</summary>
    Task<IReadOnlyList<ChangedFile>> GetChangedFilesAsync(int prId, CancellationToken cancellationToken = default);

    /// <summary>Publishes a single review comment on the pull request.</summary>
    Task PostCommentAsync(int prId, ReviewComment comment, CancellationToken cancellationToken = default);
}

/// <summary>Resolves the <see cref="IRepoConnector"/> matching the configured provider.</summary>
public interface IRepoConnectorFactory
{
    IRepoConnector Create();
}
