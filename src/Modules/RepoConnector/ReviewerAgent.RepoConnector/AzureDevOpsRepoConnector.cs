using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ReviewerAgent.RepoConnector.Contracts;
using ReviewerAgent.SharedKernel;

namespace ReviewerAgent.RepoConnector;

/// <summary>
/// Azure DevOps Git implementation of <see cref="IRepoConnector"/> over the REST API.
/// The <see cref="HttpClient"/> is configured (base address + PAT auth) by the DI layer.
/// </summary>
internal sealed class AzureDevOpsRepoConnector(HttpClient http, ILogger<AzureDevOpsRepoConnector> logger) : IRepoConnector
{
    private const string ApiVersion = "api-version=7.1";

    // Web defaults: camelCase + case-insensitive, matching Azure DevOps JSON.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http = http;
    private readonly ILogger<AzureDevOpsRepoConnector> _logger = logger;

    public string Provider => "azuredevops";

    public async Task<PullRequest> GetPullRequestAsync(int prId, CancellationToken cancellationToken = default)
    {
        var dto = await GetAsync<AdoPullRequest>($"pullrequests/{prId}?{ApiVersion}", prId, cancellationToken);
        return new PullRequest(
            dto.PullRequestId,
            dto.Title ?? string.Empty,
            dto.Description,
            StripRef(dto.SourceRefName),
            StripRef(dto.TargetRefName),
            dto.CreatedBy?.DisplayName ?? "unknown");
    }

    public async Task<IReadOnlyList<ChangedFile>> GetChangedFilesAsync(int prId, CancellationToken cancellationToken = default)
    {
        var pr = await GetPullRequestAsync(prId, cancellationToken);

        var iterations = await GetAsync<AdoCollection<AdoIteration>>(
            $"pullrequests/{prId}/iterations?{ApiVersion}", prId, cancellationToken);
        var latest = iterations.Value is { Count: > 0 } it ? it.Max(i => i.Id) : 0;
        if (latest == 0)
        {
            return [];
        }

        var changes = await GetAsync<AdoChanges>(
            $"pullrequests/{prId}/iterations/{latest}/changes?{ApiVersion}", prId, cancellationToken);

        var entries = changes.ChangeEntries ?? [];
        var files = new List<ChangedFile>(entries.Count);
        foreach (var entry in entries)
        {
            var path = entry.Item?.Path;
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            var changeType = MapChangeType(entry.ChangeType);
            string? content = changeType == ChangeType.Deleted
                ? null
                : await TryGetContentAsync(path, pr.SourceBranch, cancellationToken);

            files.Add(new ChangedFile(path, entry.OriginalPath, changeType, Diff: string.Empty, NewContent: content));
        }

        return files;
    }

    public async Task PostCommentAsync(int prId, ReviewComment comment, CancellationToken cancellationToken = default)
    {
        object thread = comment is { FilePath: not null, Line: > 0 }
            ? new
            {
                comments = new[] { new { parentCommentId = 0, content = comment.Markdown, commentType = "text" } },
                status = "active",
                threadContext = new
                {
                    filePath = comment.FilePath,
                    rightFileStart = new { line = comment.Line, offset = 1 },
                    rightFileEnd = new { line = comment.Line, offset = 1 },
                },
            }
            : new
            {
                comments = new[] { new { parentCommentId = 0, content = comment.Markdown, commentType = "text" } },
                status = "active",
            };

        using var body = new StringContent(JsonSerializer.Serialize(thread, Json), Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync($"pullrequests/{prId}/threads?{ApiVersion}", body, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new RepoConnectorException(
                $"Azure DevOps returned {(int)response.StatusCode} posting a comment on PR {prId}: {detail}");
        }
    }

    private async Task<T> GetAsync<T>(string relativeUrl, int prId, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(relativeUrl, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new PullRequestNotFoundException(prId);
        }

        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new RepoConnectorException(
                $"Azure DevOps returned {(int)response.StatusCode} for '{relativeUrl}': {detail}");
        }

        var payload = await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken);
        return payload ?? throw new RepoConnectorException($"Empty response body for '{relativeUrl}'.");
    }

    private async Task<string?> TryGetContentAsync(string path, string branch, CancellationToken cancellationToken)
    {
        var url =
            $"items?path={Uri.EscapeDataString(path)}&includeContent=true" +
            $"&versionDescriptor.versionType=Branch&versionDescriptor.version={Uri.EscapeDataString(branch)}&{ApiVersion}";
        try
        {
            using var response = await _http.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Could not fetch content for {Path} ({Status}).", path, (int)response.StatusCode);
                return null;
            }

            var item = await response.Content.ReadFromJsonAsync<AdoItem>(Json, cancellationToken);
            return item?.Content;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Failed to fetch content for {Path}.", path);
            return null;
        }
    }

    private static string StripRef(string? refName) =>
        string.IsNullOrEmpty(refName) ? string.Empty : refName.Replace("refs/heads/", string.Empty);

    private static ChangeType MapChangeType(string? changeType)
    {
        var value = changeType?.ToLowerInvariant() ?? string.Empty;
        if (value.Contains("delete")) return ChangeType.Deleted;
        if (value.Contains("rename")) return ChangeType.Renamed;
        if (value.Contains("add")) return ChangeType.Added;
        return ChangeType.Modified;
    }

    // --- Azure DevOps REST DTOs (camelCase via web defaults) ---
    private sealed record AdoPullRequest(
        int PullRequestId,
        string? Title,
        string? Description,
        string? SourceRefName,
        string? TargetRefName,
        AdoIdentity? CreatedBy);

    private sealed record AdoIdentity(string? DisplayName);

    private sealed record AdoCollection<T>(IReadOnlyList<T>? Value);

    private sealed record AdoIteration(int Id);

    private sealed record AdoChanges(IReadOnlyList<AdoChangeEntry>? ChangeEntries);

    private sealed record AdoChangeEntry(string? ChangeType, string? OriginalPath, AdoItemRef? Item);

    private sealed record AdoItemRef(string? Path);

    private sealed record AdoItem(string? Content);
}
