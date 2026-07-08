using ReviewerAgent.TaskConnector.Contracts;

namespace ReviewerAgent.TaskManager.Contracts;

/// <summary>
/// What the pull request is supposed to accomplish: the task id discovered in the PR
/// description and the resolved work item (title, description, acceptance criteria).
/// </summary>
public sealed record TaskUnderstanding(
    int PullRequestId,
    string? TaskId,
    bool Found,
    WorkItem? WorkItem)
{
    public static TaskUnderstanding NotFound(int prId, string? taskId = null)
    {
        return new(prId, taskId, false, null);
    }
}
