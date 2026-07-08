using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using ReviewerAgent.RepoConnector;
using ReviewerAgent.RepoConnector.Contracts;
using ReviewerAgent.SharedKernel;
using Xunit;

namespace ReviewerAgent.Tests;

public class RepoConnectorTests
{
    private const string PrJson =
        """
        {
          "pullRequestId": 42,
          "title": "Improve caching",
          "description": "Fixes AB#7",
          "sourceRefName": "refs/heads/feature/cache",
          "targetRefName": "refs/heads/main",
          "createdBy": { "displayName": "Ada" }
        }
        """;

    private static AzureDevOpsRepoConnector Build(Func<HttpRequestMessage, HttpResponseMessage> responder, out StubHttpMessageHandler handler)
    {
        var client = StubHttpMessageHandler.Client(
            responder, "https://dev.azure.com/org/proj/_apis/git/repositories/repo/", out handler);
        return new AzureDevOpsRepoConnector(client, NullLogger<AzureDevOpsRepoConnector>.Instance);
    }

    [Fact]
    public async Task GetPullRequestAsync_maps_metadata_and_strips_refs()
    {
        var connector = Build(_ => StubHttpMessageHandler.Json(PrJson), out _);

        var pr = await connector.GetPullRequestAsync(42);

        Assert.Equal(42, pr.Id);
        Assert.Equal("Improve caching", pr.Title);
        Assert.Equal("feature/cache", pr.SourceBranch);
        Assert.Equal("main", pr.TargetBranch);
        Assert.Equal("Ada", pr.Author);
    }

    [Fact]
    public async Task GetPullRequestAsync_throws_PullRequestNotFound_on_404()
    {
        var connector = Build(_ => StubHttpMessageHandler.Json("{}", HttpStatusCode.NotFound), out _);

        await Assert.ThrowsAsync<PullRequestNotFoundException>(() => connector.GetPullRequestAsync(42));
    }

    [Fact]
    public async Task GetChangedFilesAsync_uses_latest_iteration_and_skips_deleted_content()
    {
        var connector = Build(Route, out _);

        var files = await connector.GetChangedFilesAsync(42);

        Assert.Equal(2, files.Count);

        var added = Assert.Single(files, f => f.Path == "/src/Service.cs");
        Assert.Equal(ChangeType.Modified, added.ChangeType);
        Assert.Equal("public class Service {}", added.NewContent);

        var deleted = Assert.Single(files, f => f.Path == "/src/Old.cs");
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
                      "changeEntries": [
                        { "changeType": "edit",   "item": { "path": "/src/Service.cs" } },
                        { "changeType": "delete", "item": { "path": "/src/Old.cs" } }
                      ]
                    }
                    """);
            }

            if (path.Contains("/iterations"))
            {
                return StubHttpMessageHandler.Json("""{ "value": [ { "id": 1 }, { "id": 3 }, { "id": 2 } ] }""");
            }

            if (path.EndsWith("/items"))
            {
                return StubHttpMessageHandler.Json("""{ "content": "public class Service {}" }""");
            }

            return StubHttpMessageHandler.Json(PrJson);
        }
    }

    [Fact]
    public async Task PostCommentAsync_throws_on_error_status()
    {
        var connector = Build(_ => StubHttpMessageHandler.Json("nope", HttpStatusCode.BadRequest), out _);

        await Assert.ThrowsAsync<RepoConnectorException>(
            () => connector.PostCommentAsync(42, new ReviewComment("/src/A.cs", 10, "Looks off")));
    }

    [Fact]
    public async Task PostCommentAsync_posts_inline_thread_for_file_and_line()
    {
        string? body = null;
        // Capture the body during the request — the connector disposes it once the call returns.
        var connector = Build(req =>
        {
            body = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return StubHttpMessageHandler.Json("{}", HttpStatusCode.Created);
        }, out _);

        await connector.PostCommentAsync(42, new ReviewComment("/src/A.cs", 10, "Inline note"));

        Assert.NotNull(body);
        Assert.Contains("threadContext", body);
        Assert.Contains("/src/A.cs", body);
        Assert.Contains("\"line\":10", body);
    }
}
