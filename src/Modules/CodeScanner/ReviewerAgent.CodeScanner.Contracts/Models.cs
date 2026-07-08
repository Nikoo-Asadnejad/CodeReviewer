using ReviewerAgent.RepoConnector.Contracts;

namespace ReviewerAgent.CodeScanner.Contracts;

/// <summary>
/// Lightweight structural context extracted from a changed C# file via Roslyn.
/// Method bodies are excluded to keep the review prompt compact.
/// </summary>
public sealed record SemanticContext(
    string? Namespace,
    IReadOnlyList<string> Types,
    IReadOnlyList<string> Methods,
    IReadOnlyList<string> Interfaces,
    IReadOnlyList<string> References,
    string? XmlDocumentation)
{
    public static SemanticContext Empty { get; } = new(null, [], [], [], [], null);

    public bool IsEmpty =>
        Namespace is null && Types.Count == 0 && Methods.Count == 0 &&
        Interfaces.Count == 0 && References.Count == 0;
}

/// <summary>A changed file paired with its extracted semantic context.</summary>
public sealed record ScannedFile(ChangedFile File, SemanticContext Context);

/// <summary>The result of scanning a pull request: its metadata plus per-file analysis.</summary>
public sealed record CodeScanResult(PullRequest PullRequest, IReadOnlyList<ScannedFile> Files);
