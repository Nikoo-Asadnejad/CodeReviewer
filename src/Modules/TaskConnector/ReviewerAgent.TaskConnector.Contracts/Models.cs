namespace ReviewerAgent.TaskConnector.Contracts;

/// <summary>
/// A work item / task fetched from a task-management host (Azure DevOps Boards,
/// ClickUp, Jira, ...).
/// </summary>
public sealed record WorkItem(
    string Id,
    string Title,
    string? Description,
    string? AcceptanceCriteria,
    string? State);
