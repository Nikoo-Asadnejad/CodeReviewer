namespace ReviewerAgent.TaskConnector;

/// <summary>ClickUp connection settings. Bound from the "ClickUp" section.</summary>
public sealed class ClickUpOptions
{
    public const string SectionName = "ClickUp";

    public string BaseUrl { get; set; } = "https://api.clickup.com/api/v2";

    /// <summary>Personal API token (starts with "pk_"). Overridable via the CLICKUP_API_TOKEN env var.</summary>
    public string ApiToken { get; set; } = string.Empty;

    /// <summary>Workspace (team) id. Required only when <see cref="UseCustomTaskIds"/> is enabled.</summary>
    public string TeamId { get; set; } = string.Empty;

    public string SpaceId { get; set; } = string.Empty;

    public string ListId { get; set; } = string.Empty;

    /// <summary>
    /// When true, work item ids are treated as ClickUp custom task ids (e.g. "FLY-123") and
    /// resolved against <see cref="TeamId"/>. When false, ids are treated as native task ids.
    /// </summary>
    public bool UseCustomTaskIds { get; set; }
}
