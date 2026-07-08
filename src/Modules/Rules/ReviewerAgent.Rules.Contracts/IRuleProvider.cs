namespace ReviewerAgent.Rules.Contracts;

/// <summary>A review rule document loaded from a Markdown file.</summary>
public sealed record RuleDocument(string Name, string Content);

/// <summary>Discovers and loads the project's review rule documents.</summary>
public interface IRuleProvider
{
    Task<IReadOnlyList<RuleDocument>> LoadRulesAsync(CancellationToken cancellationToken = default);
}
