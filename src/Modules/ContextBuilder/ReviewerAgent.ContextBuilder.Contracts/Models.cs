using ReviewerAgent.CodeScanner.Contracts;
using ReviewerAgent.RepoConnector.Contracts;
using ReviewerAgent.TaskManager.Contracts;

namespace ReviewerAgent.ContextBuilder.Contracts;

/// <summary>
/// The full context handed to the prompt builder: the scanned code (PR + per-file
/// semantic analysis) and the understanding of what the task should accomplish.
/// </summary>
public sealed record ReviewContext(CodeScanResult Code, TaskUnderstanding Task)
{
    /// <summary>Convenience accessor for the pull request metadata.</summary>
    public PullRequest PullRequest => Code.PullRequest;
}
