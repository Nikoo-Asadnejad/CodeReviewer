using ReviewerAgent.ContextBuilder.Contracts;
using ReviewerAgent.PromptManager.Contracts;

namespace ReviewerAgent.Reviewer.Contracts;

/// <summary>
/// Sends the prompt and context to an LLM (via the LLM connector) and parses the
/// structured response into review findings.
/// </summary>
public interface IReviewer
{
    Task<ReviewResult> ReviewAsync(
        ReviewPrompt prompt,
        ReviewContext context,
        CancellationToken cancellationToken = default);
}
