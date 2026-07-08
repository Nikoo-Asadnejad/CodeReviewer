using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ReviewerAgent.CommentGenerator.Contracts;
using ReviewerAgent.ContextBuilder.Contracts;
using ReviewerAgent.PromptManager.Contracts;
using ReviewerAgent.Reviewer.Contracts;
using ReviewerAgent.Rules.Contracts;

namespace ReviewerAgent.Host;

/// <summary>
/// Orchestrates a single review run: build context → load rules → build prompt →
/// review with the LLM → publish comments, then stops the host.
/// </summary>
internal sealed class ReviewWorker(
    ReviewRequest request,
    IContextBuilder contextBuilder,
    IRuleProvider ruleProvider,
    IPromptManager promptManager,
    IReviewer reviewer,
    ICommentGenerator commentGenerator,
    IHostApplicationLifetime lifetime,
    ILogger<ReviewWorker> logger) : BackgroundService
{
    private readonly ReviewRequest _request = request;
    private readonly IContextBuilder _contextBuilder = contextBuilder;
    private readonly IRuleProvider _ruleProvider = ruleProvider;
    private readonly IPromptManager _promptManager = promptManager;
    private readonly IReviewer _reviewer = reviewer;
    private readonly ICommentGenerator _commentGenerator = commentGenerator;
    private readonly IHostApplicationLifetime _lifetime = lifetime;
    private readonly ILogger<ReviewWorker> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var prId = _request.PullRequestId;
        try
        {
            _logger.LogInformation("Starting review of PR #{PrId}.", prId);

            var context = await _contextBuilder.BuildAsync(prId, stoppingToken);
            var rules = await _ruleProvider.LoadRulesAsync(stoppingToken);
            var prompt = _promptManager.Build(context, rules);
            _logger.LogInformation("Prompt generated: {Chars} chars.", prompt.User.Length);

            var result = await _reviewer.ReviewAsync(prompt, context, stoppingToken);

            if (result.Findings.Count == 0)
            {
                _logger.LogInformation("No findings for PR #{PrId}; nothing to publish.", prId);
            }
            else
            {
                await _commentGenerator.PublishAsync(prId, result, stoppingToken);
            }

            _logger.LogInformation("Review of PR #{PrId} complete.", prId);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Review of PR #{PrId} was cancelled.", prId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Review of PR #{PrId} failed: {Message}", prId, ex.Message);
        }
        finally
        {
            _lifetime.StopApplication();
        }
    }
}
