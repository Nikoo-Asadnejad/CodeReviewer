using ReviewerAgent.Reviewer.Contracts;

namespace ReviewerAgent.CommentGenerator.Contracts;

/// <summary>
/// Turns review findings into pull-request comments and publishes them via the repo
/// connector, deduplicating within the run.
/// </summary>
public interface ICommentGenerator
{
    Task PublishAsync(int prId, ReviewResult result, CancellationToken cancellationToken = default);
}
