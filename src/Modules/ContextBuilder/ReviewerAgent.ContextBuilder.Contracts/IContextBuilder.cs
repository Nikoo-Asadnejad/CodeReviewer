namespace ReviewerAgent.ContextBuilder.Contracts;

/// <summary>
/// Assembles the <see cref="ReviewContext"/> for a pull request by composing the code
/// scanner and the task manager.
/// </summary>
public interface IContextBuilder
{
    Task<ReviewContext> BuildAsync(int prId, CancellationToken cancellationToken = default);
}
