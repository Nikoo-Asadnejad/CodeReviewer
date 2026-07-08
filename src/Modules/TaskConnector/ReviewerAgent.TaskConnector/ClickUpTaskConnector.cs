using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReviewerAgent.SharedKernel;
using ReviewerAgent.TaskConnector.Contracts;

namespace ReviewerAgent.TaskConnector;

/// <summary>
/// ClickUp implementation of <see cref="ITaskConnector"/> (v2 task REST API). The HttpClient is
/// configured (base address, Authorization token) by the DI layer.
/// </summary>
internal sealed class ClickUpTaskConnector(HttpClient http, IOptions<ClickUpOptions> options, ILogger<ClickUpTaskConnector> logger) : ITaskConnector
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http = http;
    private readonly ClickUpOptions _options = options.Value;
    private readonly ILogger<ClickUpTaskConnector> _logger = logger;

    public string Provider => "clickup";

    public async Task<WorkItem?> GetWorkItemAsync(string id, CancellationToken cancellationToken = default)
    {
        var requestUri = $"task/{Uri.EscapeDataString(id)}{BuildQuery()}";

        using var response = await _http.GetAsync(requestUri, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogDebug("ClickUp task {Id} not found.", id);
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new RepoConnectorException(
                $"ClickUp returned {(int)response.StatusCode} fetching task {id}: {detail}");
        }

        var dto = await response.Content.ReadFromJsonAsync<ClickUpTask>(Json, cancellationToken);
        if (dto is null)
        {
            return null;
        }

        // ClickUp has no native "acceptance criteria" field; prefer the markdown
        // description and fall back to the plain-text content.
        var description = !string.IsNullOrWhiteSpace(dto.Description) ? dto.Description : dto.TextContent;

        return new WorkItem(
            Id: dto.Id ?? id,
            Title: dto.Name ?? string.Empty,
            Description: description,
            AcceptanceCriteria: null,
            State: dto.Status?.Status);
    }

    private string BuildQuery()
    {
        if (_options.UseCustomTaskIds && !string.IsNullOrWhiteSpace(_options.TeamId))
        {
            return $"?custom_task_ids=true&team_id={Uri.EscapeDataString(_options.TeamId)}";
        }

        return string.Empty;
    }

    private sealed record ClickUpTask(
        string? Id,
        string? Name,
        string? Description,
        [property: JsonPropertyName("text_content")] string? TextContent,
        ClickUpStatus? Status);

    private sealed record ClickUpStatus(string? Status);
}
