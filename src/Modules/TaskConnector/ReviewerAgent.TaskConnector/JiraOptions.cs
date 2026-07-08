namespace ReviewerAgent.TaskConnector;

/// <summary>Jira connection settings. Bound from the "Jira" section.</summary>
public sealed class JiraOptions
{
    public const string SectionName = "Jira";

    /// <summary>Site root, e.g. "https://your-org.atlassian.net". Overridable via JIRA_BASE_URL.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Account email used for Basic auth alongside <see cref="ApiToken"/>. Overridable via JIRA_EMAIL.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>API token (Atlassian account token). Overridable via JIRA_API_TOKEN.</summary>
    public string ApiToken { get; set; } = string.Empty;

    /// <summary>
    /// Optional custom-field id holding acceptance criteria (e.g. "customfield_10001"). Jira has
    /// no native acceptance-criteria field; when set, that field is requested and mapped.
    /// </summary>
    public string AcceptanceCriteriaField { get; set; } = string.Empty;
}
