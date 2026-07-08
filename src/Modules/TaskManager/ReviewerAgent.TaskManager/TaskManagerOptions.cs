namespace ReviewerAgent.TaskManager;

/// <summary>Task-discovery settings. Bound from the "TaskManager" section.</summary>
public sealed class TaskManagerOptions
{
    public const string SectionName = "TaskManager";

    /// <summary>
    /// Regex used to extract the task id from the PR title/description. The first capture
    /// group is treated as the id. Defaults to Azure Boards mention syntax (e.g. "AB#123" or "#123").
    /// </summary>
    public string TaskIdPattern { get; set; } = @"(?:AB#|#)(\d+)";
}
