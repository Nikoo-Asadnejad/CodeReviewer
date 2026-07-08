using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ReviewerAgent.SharedKernel;
using ReviewerAgent.TaskConnector.Contracts;

namespace ReviewerAgent.TaskConnector;

/// <summary>
/// Azure DevOps Boards implementation of <see cref="ITaskConnector"/> (work items REST API).
/// </summary>
internal sealed class AzureDevOpsTaskConnector(HttpClient http, ILogger<AzureDevOpsTaskConnector> logger) : ITaskConnector
{
    private const string ApiVersion = "api-version=7.1";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http = http;
    private readonly ILogger<AzureDevOpsTaskConnector> _logger = logger;

    public string Provider => "azuredevops";

    public async Task<WorkItem?> GetWorkItemAsync(string id, CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync($"workitems/{Uri.EscapeDataString(id)}?{ApiVersion}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogDebug("Work item {Id} not found.", id);
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new RepoConnectorException(
                $"Azure DevOps returned {(int)response.StatusCode} fetching work item {id}: {detail}");
        }

        var dto = await response.Content.ReadFromJsonAsync<AdoWorkItem>(Json, cancellationToken);
        if (dto is null)
        {
            return null;
        }

        var fields = dto.Fields ?? new Dictionary<string, JsonElement>();
        return new WorkItem(
            Id: id,
            Title: GetString(fields, "System.Title") ?? string.Empty,
            Description: GetString(fields, "System.Description"),
            AcceptanceCriteria: GetString(fields, "Microsoft.VSTS.Common.AcceptanceCriteria"),
            State: GetString(fields, "System.State"));
    }

    private static string? GetString(IReadOnlyDictionary<string, JsonElement> fields, string key) =>
        fields.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private sealed record AdoWorkItem(Dictionary<string, JsonElement>? Fields);
}
