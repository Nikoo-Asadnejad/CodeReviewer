using ReviewerAgent.ContextBuilder.Contracts;
using ReviewerAgent.Rules.Contracts;

namespace ReviewerAgent.PromptManager.Contracts;

/// <summary>A system + user prompt pair ready to send to an LLM.</summary>
public sealed record ReviewPrompt(string System, string User);

/// <summary>Builds the review prompt from the review context and the loaded rule documents.</summary>
public interface IPromptManager
{
    ReviewPrompt Build(ReviewContext context, IReadOnlyList<RuleDocument> rules);
}
