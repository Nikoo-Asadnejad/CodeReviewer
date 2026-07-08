using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReviewerAgent.SharedKernel;
using ReviewerAgent.TaskConnector.Contracts;

namespace ReviewerAgent.TaskConnector;

/// <summary>
/// Jira Cloud implementation of <see cref="ITaskConnector"/> (REST API v3). The HttpClient is
/// configured (base address <c>.../rest/api/3/</c> and Basic auth) by the DI layer. Rich-text
/// fields arrive as Atlassian Document Format (ADF) and are flattened to plain text.
/// </summary>
internal sealed class JiraTaskConnector(HttpClient http, IOptions<JiraOptions> options, ILogger<JiraTaskConnector> logger) : ITaskConnector
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http = http;
    private readonly JiraOptions _options = options.Value;
    private readonly ILogger<JiraTaskConnector> _logger = logger;

    public string Provider => "jira";

    public async Task<WorkItem?> GetWorkItemAsync(string id, CancellationToken cancellationToken = default)
    {
        var acField = _options.AcceptanceCriteriaField;
        var fields = string.IsNullOrWhiteSpace(acField)
            ? "summary,status,description"
            : $"summary,status,description,{acField}";

        using var response = await _http.GetAsync(
            $"issue/{Uri.EscapeDataString(id)}?fields={fields}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogDebug("Jira issue {Id} not found.", id);
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new RepoConnectorException(
                $"Jira returned {(int)response.StatusCode} fetching issue {id}: {detail}");
        }

        using var doc = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        var root = doc.RootElement;
        if (!root.TryGetProperty("fields", out var f) || f.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var key = root.TryGetProperty("key", out var k) && k.ValueKind == JsonValueKind.String ? k.GetString()! : id;
        var summary = f.TryGetProperty("summary", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() : null;
        var state = f.TryGetProperty("status", out var st) && st.ValueKind == JsonValueKind.Object
            && st.TryGetProperty("name", out var sn) && sn.ValueKind == JsonValueKind.String
            ? sn.GetString()
            : null;

        var description = f.TryGetProperty("description", out var d) ? ExtractText(d) : null;
        string? acceptance = !string.IsNullOrWhiteSpace(acField) && f.TryGetProperty(acField, out var ac)
            ? ExtractText(ac)
            : null;

        return new WorkItem(
            Id: key,
            Title: summary ?? string.Empty,
            Description: description,
            AcceptanceCriteria: acceptance,
            State: state);
    }

    /// <summary>
    /// Flattens a Jira field into plain text. Handles a plain JSON string, or an ADF document
    /// (a tree of nodes) by concatenating its "text" leaves with block-level line breaks.
    /// </summary>
    private static string? ExtractText(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.Object:
                var sb = new StringBuilder();
                AppendAdf(element, sb);
                var text = sb.ToString().Trim();
                return text.Length == 0 ? null : text;
            default:
                return null;
        }
    }

    private static void AppendAdf(JsonElement node, StringBuilder sb)
    {
        if (node.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var type = node.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;
        if (type == "text" && node.TryGetProperty("text", out var txt) && txt.ValueKind == JsonValueKind.String)
        {
            sb.Append(txt.GetString());
        }
        else if (type == "hardBreak")
        {
            sb.Append('\n');
        }

        if (node.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in content.EnumerateArray())
            {
                AppendAdf(child, sb);
            }
        }

        // Close block-level nodes with a newline so paragraphs/headings/list items stay separated.
        if (type is "paragraph" or "heading" or "listItem" or "blockquote" or "codeBlock")
        {
            sb.Append('\n');
        }
    }
}
