using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using ReviewerAgent.RepoConnector;
using ReviewerAgent.RepoConnector.Contracts;
using ReviewerAgent.SharedKernel;
using Xunit;

namespace ReviewerAgent.Tests;

public class GitHubRepoConnectorTests
{
    private const string PrJson =
        """
        {
          "number": 42,
          "title": "Improve caching",
          "body": "Fixes #7",
          "head": { "ref": "feature/cache", "sha": "abc123" },
          "base": { "ref": "main" },
          "user": { "login": "ada" }
        }
        """;

    private static GitHubRepoConnector Build(Func<HttpRequestMessage, HttpResponseMessage> responder, out StubHttpMessageHandler handler)
    {
        var client = StubHttpMessageHandler.Client(responder, "https://api.github.com/repos/owner/repo/", out handler);
        return new GitHubRepoConnector(client, NullLogger<GitHubRepoConnector>.Instance);
    }

    [Fact]
    public async Task GetPullRequestAsync_maps_metadata()
    {
        var connector = Build(_ => StubHttpMessageHandler.Json(PrJson), out _);

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
    public async Task GetChangedFilesAsync_maps_patch_decodes_content_and_skips_deleted()
    {
        var connector = Build(Route, out _);

        var files = await connector.GetChangedFilesAsync(42);

        Assert.Equal(2, files.Count);

        var modified = Assert.Single(files, f => f.Path == "src/Service.cs");
        Assert.Equal(ChangeType.Modified, modified.ChangeType);
        Assert.Equal("@@ -1 +1 @@", modified.Diff);
        Assert.Equal("public class Service {}", modified.NewContent);

        var removed = Assert.Single(files, f => f.Path == "src/Old.cs");
        Assert.Equal(ChangeType.Deleted, removed.ChangeType);
        Assert.Null(removed.NewContent);

        static HttpResponseMessage Route(HttpRequestMessage req)
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path.EndsWith("/files"))
            {
                return StubHttpMessageHandler.Json(
                    """
                    [
                      { "filename": "src/Service.cs", "status": "modified", "patch": "@@ -1 +1 @@" },
                      { "filename": "src/Old.cs", "status": "removed", "patch": "@@ -1 +0 @@" }
                    ]
                    """);
            }

            if (path.Contains("/contents/"))
            {
                // base64 of "public class Service {}"
                return StubHttpMessageHandler.Json(
                    """{ "content": "cHVibGljIGNsYXNzIFNlcnZpY2Uge30=", "encoding": "base64" }""");
            }

            return StubHttpMessageHandler.Json(PrJson);
        }
    }

    [Fact]
    public async Task PostCommentAsync_posts_inline_review_comment_with_commit_id()
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

            return StubHttpMessageHandler.Json(PrJson);
        }, out _);

        await connector.PostCommentAsync(42, new ReviewComment("src/A.cs", 10, "Inline note"));

        Assert.NotNull(body);
        Assert.EndsWith("/pulls/42/comments", postedPath);
        Assert.Contains("\"commit_id\":\"abc123\"", body);
        Assert.Contains("\"line\":10", body);
        Assert.Contains("src/A.cs", body);
    }

    [Fact]
    public async Task PostCommentAsync_posts_general_issue_comment_without_file()
    {
        string? postedPath = null;
        var connector = Build(req =>
        {
            postedPath = req.RequestUri!.AbsolutePath;
            return StubHttpMessageHandler.Json("{}", HttpStatusCode.Created);
        }, out _);

        await connector.PostCommentAsync(42, new ReviewComment(null, 0, "General note"));

        Assert.EndsWith("/issues/42/comments", postedPath);
    }
}
