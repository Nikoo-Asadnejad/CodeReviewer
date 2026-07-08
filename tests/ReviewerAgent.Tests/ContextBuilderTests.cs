using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReviewerAgent.CodeScanner.Contracts;
using ReviewerAgent.RepoConnector.Contracts;
using ReviewerAgent.TaskManager.Contracts;
using Xunit;

namespace ReviewerAgent.Tests;

public class ContextBuilderTests
{
    [Fact]
    public async Task Build_composes_code_scan_and_task_understanding()
    {
        var pr = new PullRequest(5, "t", null, "feature", "main", "me");
        var scan = new CodeScanResult(pr, []);
        var task = new TaskUnderstanding(5, "42", true, null);

        var scanner = new Mock<ICodeScanner>();
        scanner.Setup(s => s.ScanAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(scan);
        var taskManager = new Mock<ITaskManager>();
        taskManager.Setup(t => t.UnderstandAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(task);

        var builder = new ReviewerAgent.ContextBuilder.ContextBuilder(
            scanner.Object, taskManager.Object, NullLogger<ReviewerAgent.ContextBuilder.ContextBuilder>.Instance);

        var context = await builder.BuildAsync(5);

        Assert.Same(scan, context.Code);
        Assert.Same(task, context.Task);
        Assert.Equal(5, context.PullRequest.Id);
    }
}
