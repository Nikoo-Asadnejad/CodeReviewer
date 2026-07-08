namespace ReviewerAgent.SharedKernel;

/// <summary>Base type for all reviewer-agent domain exceptions.</summary>
public abstract class ReviewerException : Exception
{
    protected ReviewerException(string message) : base(message) { }
    protected ReviewerException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>An external repository host (e.g. Azure DevOps) returned an unexpected error.</summary>
public sealed class RepoConnectorException : ReviewerException
{
    public RepoConnectorException(string message) : base(message) { }
    public RepoConnectorException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>The requested pull request could not be found in the repository host.</summary>
public sealed class PullRequestNotFoundException : ReviewerException
{
    public int PullRequestId { get; }

    public PullRequestNotFoundException(int pullRequestId)
        : base($"Pull request {pullRequestId} was not found.")
        => PullRequestId = pullRequestId;
}

/// <summary>The requested work item / task could not be found in the task host.</summary>
public sealed class TaskNotFoundException : ReviewerException
{
    public string TaskId { get; }

    public TaskNotFoundException(string taskId)
        : base($"Task '{taskId}' was not found.")
        => TaskId = taskId;
}

/// <summary>An LLM provider returned an error or invalid response.</summary>
public sealed class LlmException : ReviewerException
{
    public LlmException(string message) : base(message) { }
    public LlmException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>The configured LLM did not respond within the allotted timeout.</summary>
public sealed class LlmTimeoutException : ReviewerException
{
    public LlmTimeoutException(string message) : base(message) { }
    public LlmTimeoutException(string message, Exception inner) : base(message, inner) { }
}
