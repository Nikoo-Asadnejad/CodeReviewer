using Microsoft.Extensions.Logging;
using ReviewerAgent.CodeScanner.Contracts;
using ReviewerAgent.ContextBuilder.Contracts;
using ReviewerAgent.TaskManager.Contracts;

namespace ReviewerAgent.ContextBuilder;

/// <summary>Composes the code scanner and task manager into a single <see cref="ReviewContext"/>.</summary>
internal sealed class ContextBuilder(ICodeScanner scanner, ITaskManager taskManager, ILogger<ContextBuilder> logger) : IContextBuilder
{
    private readonly ICodeScanner _scanner = scanner;
    private readonly ITaskManager _taskManager = taskManager;
    private readonly ILogger<ContextBuilder> _logger = logger;

    public async Task<ReviewContext> BuildAsync(int prId, CancellationToken cancellationToken = default)
    {
        var code = await _scanner.ScanAsync(prId, cancellationToken);
        var task = await _taskManager.UnderstandAsync(prId, cancellationToken);

        _logger.LogInformation("Context built for PR #{PrId}: {Files} file(s), task linked: {Linked}.",
            prId, code.Files.Count, task.Found);

        return new ReviewContext(code, task);
    }
}
