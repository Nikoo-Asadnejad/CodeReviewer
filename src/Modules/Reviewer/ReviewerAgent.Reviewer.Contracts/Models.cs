namespace ReviewerAgent.Reviewer.Contracts;

/// <summary>Severity of a review finding, ordered from most to least urgent.</summary>
public enum Severity : byte
{
    Critical,
    High,
    Medium,
    Low,
    Suggestion
}

/// <summary>A single issue raised by the AI reviewer, targeting a specific file and line.</summary>
public sealed record Finding(
    string Title,
    Severity Severity,
    string Category,
    string File,
    int Line,
    string Comment,
    string? Recommendation,
    double Confidence);

/// <summary>The structured outcome of a review: a summary plus zero or more findings.</summary>
public sealed record ReviewResult(string Summary, IReadOnlyList<Finding> Findings)
{
    public static ReviewResult Empty { get; } = new(string.Empty, []);
}
