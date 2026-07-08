using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using ReviewerAgent.RepoConnector.Contracts;
using ReviewerAgent.SharedKernel;

namespace ReviewerAgent.RepoConnector;

/// <summary>
/// GitLab implementation of <see cref="IRepoConnector"/> over the v4 REST API. Pull requests
/// map to merge requests, addressed by their project-scoped <c>iid</c>. The <see cref="HttpClient"/>
/// is configured (base address <c>.../projects/{id}/</c> and PRIVATE-TOKEN auth) by the DI layer.
/// </summary>
internal sealed class GitLabRepoConnector(HttpClient http, ILogger<GitLabRepoConnector> logger) : IRepoConnector
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http = http;
    private readonly ILogger<GitLabRepoConnector> _logger = logger;

    public string Provider => "gitlab";

    public async Task<PullRequest> GetPullRequestAsync(int prId, CancellationToken cancellationToken = default)
    {
        var dto = await GetAsync<GlMergeRequest>($"merge_requests/{prId}", prId, cancellationToken);
        return new PullRequest(
            dto.Iid,
            dto.Title ?? string.Empty,
            dto.Description,
            dto.SourceBranch ?? string.Empty,
            dto.TargetBranch ?? string.Empty,
            dto.Author?.Username ?? "unknown");
    }

    public async Task<IReadOnlyList<ChangedFile>> GetChangedFilesAsync(int prId, CancellationToken cancellationToken = default)
    {
        var mr = await GetAsync<GlMergeRequest>($"merge_requests/{prId}/changes", prId, cancellationToken);
        var sourceBranch = mr.SourceBranch ?? string.Empty;

        var changes = mr.Changes ?? [];
        var files = new List<ChangedFile>(changes.Count);
        foreach (var change in changes)
        {
            var path = change.NewPath ?? change.OldPath;
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            var changeType = MapChangeType(change);
            string? content = changeType == ChangeType.Deleted
                ? null
                : await TryGetContentAsync(path, sourceBranch, cancellationToken);

            files.Add(new ChangedFile(
                path,
                change.RenamedFile == true ? change.OldPath : null,
                changeType,
                Diff: change.Diff ?? string.Empty,
                NewContent: content));
        }

        return files;
    }

    public async Task PostCommentAsync(int prId, ReviewComment comment, CancellationToken cancellationToken = default)
    {
        // Inline comments are posted as positioned discussions, which need the merge request's
        // diff refs (base/head/start shas). Anything else becomes a plain merge-request note.
        if (comment is { FilePath: not null, Line: > 0 })
        {
            var mr = await GetAsync<GlMergeRequest>($"merge_requests/{prId}", prId, cancellationToken);
            var refs = mr.DiffRefs;
            if (refs is { BaseSha: not null, HeadSha: not null, StartSha: not null })
            {
                var discussion = new
                {
                    body = comment.Markdown,
                    position = new
                    {
                        position_type = "text",
                        base_sha = refs.BaseSha,
                        head_sha = refs.HeadSha,
                        start_sha = refs.StartSha,
                        new_path = comment.FilePath,
                        old_path = comment.FilePath,
                        new_line = comment.Line,
                    },
                };
                await PostAsync($"merge_requests/{prId}/discussions", discussion, prId, cancellationToken);
                return;
            }

            _logger.LogWarning(
                "Merge request !{PrId} has no diff refs; posting comment for {Path} as a general note.",
                prId, comment.FilePath);
        }

        await PostAsync($"merge_requests/{prId}/notes", new { body = comment.Markdown }, prId, cancellationToken);
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
                $"GitLab returned {(int)response.StatusCode} for '{relativeUrl}': {detail}");
        }

        var payload = await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken);
        return payload ?? throw new RepoConnectorException($"Empty response body for '{relativeUrl}'.");
    }

    private async Task PostAsync(string relativeUrl, object body, int prId, CancellationToken cancellationToken)
    {
        using var content = JsonContent.Create(body, options: Json);
        using var response = await _http.PostAsync(relativeUrl, content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new RepoConnectorException(
                $"GitLab returned {(int)response.StatusCode} posting a comment on MR !{prId}: {detail}");
        }
    }

    private async Task<string?> TryGetContentAsync(string path, string branch, CancellationToken cancellationToken)
    {
        // The file path is a single, fully URL-encoded path parameter (slashes included).
        var url = $"repository/files/{Uri.EscapeDataString(path)}?ref={Uri.EscapeDataString(branch)}";
        try
        {
            using var response = await _http.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Could not fetch content for {Path} ({Status}).", path, (int)response.StatusCode);
                return null;
            }

            var item = await response.Content.ReadFromJsonAsync<GlFile>(Json, cancellationToken);
            if (item?.Content is null || !string.Equals(item.Encoding, "base64", StringComparison.OrdinalIgnoreCase))
            {
                return item?.Content;
            }

            return Encoding.UTF8.GetString(Convert.FromBase64String(item.Content));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Failed to fetch content for {Path}.", path);
            return null;
        }
    }

    private static ChangeType MapChangeType(GlChange change)
    {
        if (change.DeletedFile == true) return ChangeType.Deleted;
        if (change.RenamedFile == true) return ChangeType.Renamed;
        if (change.NewFile == true) return ChangeType.Added;
        return ChangeType.Modified;
    }

    // --- GitLab REST DTOs ---
    private sealed record GlMergeRequest(
        int Iid,
        string? Title,
        string? Description,
        [property: JsonPropertyName("source_branch")] string? SourceBranch,
        [property: JsonPropertyName("target_branch")] string? TargetBranch,
        GlAuthor? Author,
        IReadOnlyList<GlChange>? Changes,
        [property: JsonPropertyName("diff_refs")] GlDiffRefs? DiffRefs);

    private sealed record GlAuthor(string? Username);

    private sealed record GlChange(
        [property: JsonPropertyName("old_path")] string? OldPath,
        [property: JsonPropertyName("new_path")] string? NewPath,
        string? Diff,
        [property: JsonPropertyName("new_file")] bool? NewFile,
        [property: JsonPropertyName("deleted_file")] bool? DeletedFile,
        [property: JsonPropertyName("renamed_file")] bool? RenamedFile);

    private sealed record GlDiffRefs(
        [property: JsonPropertyName("base_sha")] string? BaseSha,
        [property: JsonPropertyName("head_sha")] string? HeadSha,
        [property: JsonPropertyName("start_sha")] string? StartSha);

    private sealed record GlFile(string? Content, string? Encoding);
}
