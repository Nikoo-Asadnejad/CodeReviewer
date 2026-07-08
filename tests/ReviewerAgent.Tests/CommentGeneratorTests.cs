using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReviewerAgent.CommentGenerator;
using ReviewerAgent.RepoConnector.Contracts;
using ReviewerAgent.Reviewer.Contracts;
using Xunit;

namespace ReviewerAgent.Tests;

public class CommentGeneratorTests
{
    private static CommentGenerator.CommentGenerator Build(IRepoConnector repo) =>
        new(repo, NullLogger<CommentGenerator.CommentGenerator>.Instance);

    private static Finding Finding(string title, string file, int line) =>
        new(title, Severity.High, "Security", file, line, "comment", "fix", 0.9);

    [Fact]
    public async Task Duplicate_findings_are_published_once()
    {
        var repo = new Mock<IRepoConnector>();
        var result = new ReviewResult("s", [Finding("Same", "a.cs", 10), Finding("Same", "a.cs", 10)]);

        await Build(repo.Object).PublishAsync(1, result);

        repo.Verify(r => r.PostCommentAsync(1, It.IsAny<ReviewComment>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Distinct_findings_each_publish()
    {
        var repo = new Mock<IRepoConnector>();
        var result = new ReviewResult("s", [Finding("A", "a.cs", 10), Finding("B", "a.cs", 20)]);

        await Build(repo.Object).PublishAsync(1, result);

        repo.Verify(r => r.PostCommentAsync(1, It.IsAny<ReviewComment>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task One_failure_does_not_stop_the_rest()
    {
        var repo = new Mock<IRepoConnector>();
        repo.SetupSequence(r => r.PostCommentAsync(1, It.IsAny<ReviewComment>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"))
            .Returns(Task.CompletedTask);

        var result = new ReviewResult("s", [Finding("A", "a.cs", 10), Finding("B", "b.cs", 20)]);

        await Build(repo.Object).PublishAsync(1, result); // must not throw

        repo.Verify(r => r.PostCommentAsync(1, It.IsAny<ReviewComment>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
