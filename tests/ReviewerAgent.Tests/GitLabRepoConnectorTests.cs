using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using ReviewerAgent.RepoConnector;
using ReviewerAgent.RepoConnector.Contracts;
using ReviewerAgent.SharedKernel;
using Xunit;

namespace ReviewerAgent.Tests;

public class GitLabRepoConnectorTests
{
    private const string MrJson =
        """
        {
          "iid": 42,
          "title": "Improve caching",
          "description": "Fixes #7",
          "source_branch": "feature/cache",
          "target_branch": "main",
          "author": { "username": "ada" },
          "diff_refs": { "base_sha": "b1", "head_sha": "h1", "start_sha": "s1" }
        }
        """;

    private static GitLabRepoConnector Build(Func<HttpRequestMessage, HttpResponseMessage> responder, out StubHttpMessageHandler handler)
    {
        var client = StubHttpMessageHandler.Client(responder, "https://gitlab.com/api/v4/projects/123/", out handler);
        return new GitLabRepoConnector(client, NullLogger<GitLabRepoConnector>.Instance);
    }

    [Fact]
    public async Task GetPullRequestAsync_maps_merge_request_metadata()
    {
        var connector = Build(_ => StubHttpMessageHandler.Json(MrJson), out _);

        var pr = await connector.GetPullRequestAsync(42);

        Assert.Equal(42, pr.Id);
        Assert.Equal("Improve caching", pr.Title);
        Assert.Equal("feature/cache", pr.SourceBranch);
        Assert.Equal("main", pr.TargetBranch);
        Assert.Equal("ada", pr.Author);
    }

    [Fact]
    public async Task GetPullRequestAsync_throws_PullRequestNotFound_on_404()
    {
        var connector = Build(_ => StubHttpMessageHandler.Json("{}", HttpStatusCode.NotFound), out _);

        await Assert.ThrowsAsync<PullRequestNotFoundException>(() => connector.GetPullRequestAsync(42));
    }

    [Fact]
    public async Task GetChangedFilesAsync_maps_changes_decodes_content_and_skips_deleted()
    {
        var connector = Build(Route, out _);

        var files = await connector.GetChangedFilesAsync(42);

        Assert.Equal(2, files.Count);

        var modified = Assert.Single(files, f => f.Path == "src/Service.cs");
        Assert.Equal(ChangeType.Modified, modified.ChangeType);
        Assert.Equal("@@ diff @@", modified.Diff);
        Assert.Equal("public class Service {}", modified.NewContent);

        var deleted = Assert.Single(files, f => f.Path == "src/Old.cs");
        Assert.Equal(ChangeType.Deleted, deleted.ChangeType);
        Assert.Null(deleted.NewContent);

        static HttpResponseMessage Route(HttpRequestMessage req)
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path.Contains("/changes"))
            {
                return StubHttpMessageHandler.Json(
                    """
                    {
                      "iid": 42,
                      "source_branch": "feature/cache",
                      "changes": [
                        { "new_path": "src/Service.cs", "old_path": "src/Service.cs", "diff": "@@ diff @@" },
                        { "new_path": "src/Old.cs", "old_path": "src/Old.cs", "deleted_file": true, "diff": "" }
                      ]
                    }
                    """);
            }

            if (path.Contains("/repository/files/"))
            {
                return StubHttpMessageHandler.Json(
                    """{ "content": "cHVibGljIGNsYXNzIFNlcnZpY2Uge30=", "encoding": "base64" }""");
            }

            return StubHttpMessageHandler.Json(MrJson);
        }
    }

    [Fact]
    public async Task PostCommentAsync_posts_positioned_discussion_for_inline_comment()
    {
        string? body = null;
        string? postedPath = null;
        var connector = Build(req =>
        {
            if (req.Method == HttpMethod.Post)
            {
                postedPath = req.RequestUri!.AbsolutePath;
                body = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                return StubHttpMessageHandler.Json("{}", HttpStatusCode.Created);
            }

            return StubHttpMessageHandler.Json(MrJson);
        }, out _);

        await connector.PostCommentAsync(42, new ReviewComment("src/A.cs", 10, "Inline note"));

        Assert.NotNull(body);
        Assert.EndsWith("/merge_requests/42/discussions", postedPath);
        Assert.Contains("\"head_sha\":\"h1\"", body);
        Assert.Contains("\"new_line\":10", body);
        Assert.Contains("src/A.cs", body);
    }

    [Fact]
    public async Task PostCommentAsync_posts_note_without_file()
    {
        string? postedPath = null;
        var connector = Build(req =>
        {
            postedPath = req.RequestUri!.AbsolutePath;
            return StubHttpMessageHandler.Json("{}", HttpStatusCode.Created);
        }, out _);

        await connector.PostCommentAsync(42, new ReviewComment(null, 0, "General note"));

        Assert.EndsWith("/merge_requests/42/notes", postedPath);
    }
}
