namespace ReviewerAgent.TaskManager.Contracts;

/// <summary>
/// Determines what a pull request should accomplish by reading its description (via the
/// repo connector) to find the task id, then fetching the work item (via the task connector).
/// </summary>
public interface ITaskManager
{
    Task<TaskUnderstanding> UnderstandAsync(int prId, CancellationToken cancellationToken = default);
}
