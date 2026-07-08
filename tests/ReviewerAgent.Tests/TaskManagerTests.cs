using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ReviewerAgent.RepoConnector.Contracts;
using ReviewerAgent.TaskConnector.Contracts;
using ReviewerAgent.TaskManager;
using Xunit;

namespace ReviewerAgent.Tests;

public class TaskManagerTests
{
    private static ReviewerAgent.TaskManager.TaskManager Build(IRepoConnector repo, ITaskConnector tasks) =>
        new(repo, tasks, Options.Create(new TaskManagerOptions()),
            NullLogger<ReviewerAgent.TaskManager.TaskManager>.Instance);

    private static Mock<IRepoConnector> RepoWithDescription(string description)
    {
        var repo = new Mock<IRepoConnector>();
        repo.Setup(r => r.GetPullRequestAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PullRequest(7, "Title", description, "feature", "main", "me"));
        return repo;
    }

    [Fact]
    public async Task Finds_task_referenced_in_description()
    {
        var repo = RepoWithDescription("Implements AB#123 per spec.");
        var tasks = new Mock<ITaskConnector>();
        tasks.Setup(t => t.GetWorkItemAsync("123", It.IsAny<CancellationToken>()))
             .ReturnsAsync(new WorkItem("123", "Do the thing", "desc", "criteria", "Active"));

        var result = await Build(repo.Object, tasks.Object).UnderstandAsync(7);

        Assert.True(result.Found);
        Assert.Equal("123", result.TaskId);
        Assert.Equal("Do the thing", result.WorkItem!.Title);
    }

    [Fact]
    public async Task Returns_not_found_when_no_task_id_in_description()
    {
        var repo = RepoWithDescription("No task reference here.");
        var tasks = new Mock<ITaskConnector>();

        var result = await Build(repo.Object, tasks.Object).UnderstandAsync(7);

        Assert.False(result.Found);
        Assert.Null(result.TaskId);
        tasks.Verify(t => t.GetWorkItemAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Returns_not_found_when_work_item_missing()
    {
        var repo = RepoWithDescription("Fixes #99");
        var tasks = new Mock<ITaskConnector>();
        tasks.Setup(t => t.GetWorkItemAsync("99", It.IsAny<CancellationToken>()))
             .ReturnsAsync((WorkItem?)null);

        var result = await Build(repo.Object, tasks.Object).UnderstandAsync(7);

        Assert.False(result.Found);
        Assert.Equal("99", result.TaskId);
    }

    [Fact]
    public async Task Degrades_to_unlinked_when_task_connector_throws()
    {
        var repo = RepoWithDescription("Fixes #99");
        var tasks = new Mock<ITaskConnector>();
        tasks.Setup(t => t.GetWorkItemAsync("99", It.IsAny<CancellationToken>()))
             .ThrowsAsync(new HttpRequestException("task host unreachable"));

        // The task host is best-effort: a connection failure must not fail the review.
        var result = await Build(repo.Object, tasks.Object).UnderstandAsync(7);

        Assert.False(result.Found);
        Assert.Equal("99", result.TaskId);
        Assert.Null(result.WorkItem);
    }

    [Fact]
    public async Task Propagates_cancellation_from_task_connector()
    {
        var repo = RepoWithDescription("Fixes #99");
        var tasks = new Mock<ITaskConnector>();
        tasks.Setup(t => t.GetWorkItemAsync("99", It.IsAny<CancellationToken>()))
             .ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Build(repo.Object, tasks.Object).UnderstandAsync(7));
    }
}
