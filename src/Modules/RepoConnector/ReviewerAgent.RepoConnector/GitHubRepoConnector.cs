using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ReviewerAgent.RepoConnector.Contracts;
using ReviewerAgent.SharedKernel;

namespace ReviewerAgent.RepoConnector;

/// <summary>
/// GitHub implementation of <see cref="IRepoConnector"/> over the REST API (v2022-11-28).
/// The <see cref="HttpClient"/> is configured (base address <c>.../repos/{owner}/{repo}/</c>,
/// bearer auth, and the required User-Agent/Accept headers) by the DI layer.
/// </summary>
internal sealed class GitHubRepoConnector(HttpClient http, ILogger<GitHubRepoConnector> logger) : IRepoConnector
{
    private const int PageSize = 100;

    // Web defaults: camelCase + case-insensitive. GitHub fields are snake_case, so each DTO
    // property carries an explicit name where it differs.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http = http;
    private readonly ILogger<GitHubRepoConnector> _logger = logger;

    public string Provider => "github";

    public async Task<PullRequest> GetPullRequestAsync(int prId, CancellationToken cancellationToken = default)
    {
        var dto = await GetAsync<GhPullRequest>($"pulls/{prId}", prId, cancellationToken);
        return new PullRequest(
            dto.Number,
            dto.Title ?? string.Empty,
            dto.Body,
            dto.Head?.Ref ?? string.Empty,
            dto.Base?.Ref ?? string.Empty,
            dto.User?.Login ?? "unknown");
    }

    public async Task<IReadOnlyList<ChangedFile>> GetChangedFilesAsync(int prId, CancellationToken cancellationToken = default)
    {
        var pr = await GetPullRequestAsync(prId, cancellationToken);

        var files = new List<ChangedFile>();
        for (var page = 1; ; page++)
        {
            var batch = await GetAsync<IReadOnlyList<GhFile>>(
                $"pulls/{prId}/files?per_page={PageSize}&page={page}", prId, cancellationToken);
            if (batch.Count == 0)
            {
                break;
            }

            foreach (var file in batch)
            {
                if (string.IsNullOrWhiteSpace(file.Filename))
                {
                    continue;
                }

                var changeType = MapChangeType(file.Status);
                string? content = changeType == ChangeType.Deleted
                    ? null
                    : await TryGetContentAsync(file.Filename, pr.SourceBranch, cancellationToken);

                files.Add(new ChangedFile(
                    file.Filename,
                    file.PreviousFilename,
                    changeType,
                    Diff: file.Patch ?? string.Empty,
                    NewContent: content));
            }

            if (batch.Count < PageSize)
            {
                break;
            }
        }

        return files;
    }

    public async Task PostCommentAsync(int prId, ReviewComment comment, CancellationToken cancellationToken = default)
    {
        // Inline review comments need the commit they anchor to; a general PR comment is just
        // an issue comment. Fall back to a general comment when no file/line is supplied.
        if (comment is { FilePath: not null, Line: > 0 })
        {
            var pr = await GetAsync<GhPullRequest>($"pulls/{prId}", prId, cancellationToken);
            var commitId = pr.Head?.Sha;
            if (!string.IsNullOrEmpty(commitId))
            {
                var inline = new
                {
                    body = comment.Markdown,
                    commit_id = commitId,
                    path = comment.FilePath,
                    line = comment.Line,
                    side = "RIGHT",
                };
                await PostAsync($"pulls/{prId}/comments", inline, prId, cancellationToken);
                return;
            }

            _logger.LogWarning(
                "PR #{PrId} has no head commit sha; posting comment for {Path} as a general comment.",
                prId, comment.FilePath);
        }

        await PostAsync($"issues/{prId}/comments", new { body = comment.Markdown }, prId, cancellationToken);
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
                $"GitHub returned {(int)response.StatusCode} for '{relativeUrl}': {detail}");
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
                $"GitHub returned {(int)response.StatusCode} posting a comment on PR {prId}: {detail}");
        }
    }

    private async Task<string?> TryGetContentAsync(string path, string branch, CancellationToken cancellationToken)
    {
        var url = $"contents/{EscapePath(path)}?ref={Uri.EscapeDataString(branch)}";
        try
        {
            using var response = await _http.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Could not fetch content for {Path} ({Status}).", path, (int)response.StatusCode);
                return null;
            }

            var item = await response.Content.ReadFromJsonAsync<GhContent>(Json, cancellationToken);
            if (item?.Content is null || !string.Equals(item.Encoding, "base64", StringComparison.OrdinalIgnoreCase))
            {
                return item?.Content;
            }

            return DecodeBase64(item.Content);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Failed to fetch content for {Path}.", path);
            return null;
        }
    }

    // GitHub returns base64 content split across lines; strip whitespace before decoding.
    private static string DecodeBase64(string value)
    {
        var cleaned = value.Replace("\n", string.Empty).Replace("\r", string.Empty);
        return Encoding.UTF8.GetString(Convert.FromBase64String(cleaned));
    }

    // Preserve path separators (the contents API is segment-based) but escape each segment.
    private static string EscapePath(string path) =>
        string.Join('/', path.Split('/').Select(Uri.EscapeDataString));

    private static ChangeType MapChangeType(string? status) => status?.ToLowerInvariant() switch
    {
        "removed" => ChangeType.Deleted,
        "renamed" => ChangeType.Renamed,
        "added" => ChangeType.Added,
        _ => ChangeType.Modified,
    };

    // --- GitHub REST DTOs ---
    private sealed record GhPullRequest(int Number, string? Title, string? Body, GhRef? Head, GhRef? Base, GhUser? User);

    private sealed record GhRef(string? Ref, string? Sha);

    private sealed record GhUser(string? Login);

    private sealed record GhFile(string? Filename, string? Status, string? Patch, string? PreviousFilename);

    private sealed record GhContent(string? Content, string? Encoding);
}
