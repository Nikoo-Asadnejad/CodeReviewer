using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReviewerAgent.RepoConnector.Contracts;
using ReviewerAgent.TaskConnector.Contracts;
using ReviewerAgent.TaskManager.Contracts;

namespace ReviewerAgent.TaskManager;

/// <summary>
/// Reads the PR description (via the repo connector) to find the linked task id, then
/// fetches the work item (via the task connector) to understand what the PR should accomplish.
/// </summary>
internal sealed class TaskManager(
    IRepoConnector repo,
    ITaskConnector tasks,
    IOptions<TaskManagerOptions> options,
    ILogger<TaskManager> logger) : ITaskManager
{
    private readonly IRepoConnector _repo = repo;
    private readonly ITaskConnector _tasks = tasks;
    private readonly Regex _taskIdRegex = new(options.Value.TaskIdPattern,
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private readonly ILogger<TaskManager> _logger = logger;

    public async Task<TaskUnderstanding> UnderstandAsync(int prId, CancellationToken cancellationToken = default)
    {
        var pr = await _repo.GetPullRequestAsync(prId, cancellationToken);
        var taskId = ExtractTaskId($"{pr.Title}\n{pr.Description}");

        if (taskId is null)
        {
            _logger.LogInformation("No task id found in PR #{PrId} description.", prId);
            return TaskUnderstanding.NotFound(prId);
        }

        WorkItem? workItem;
        try
        {
            workItem = await _tasks.GetWorkItemAsync(taskId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The task host is best-effort context, not a hard dependency: if it is
            // unreachable or errors, continue the review with the task simply unlinked.
            _logger.LogWarning(ex,
                "Could not reach the task connector for task {TaskId} (PR #{PrId}); continuing without a linked task.",
                taskId, prId);
            return TaskUnderstanding.NotFound(prId, taskId);
        }

        if (workItem is null)
        {
            _logger.LogWarning("Task id {TaskId} referenced by PR #{PrId} was not found.", taskId, prId);
            return TaskUnderstanding.NotFound(prId, taskId);
        }

        _logger.LogInformation("PR #{PrId} linked to task {TaskId}: '{Title}'.", prId, taskId, workItem.Title);
        return new TaskUnderstanding(prId, taskId, true, workItem);
    }

    private string? ExtractTaskId(string text)
    {
        var match = _taskIdRegex.Match(text);
        if (!match.Success)
        {
            return null;
        }

        return match.Groups.Count > 1 && match.Groups[1].Success ? match.Groups[1].Value : match.Value;
    }
}
