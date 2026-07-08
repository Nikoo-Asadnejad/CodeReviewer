namespace ReviewerAgent.RepoConnector.Contracts;

/// <summary>How a file was changed within a pull request.</summary>
public enum ChangeType : byte
{
    Added,
    Modified,
    Deleted,
    Renamed
}

/// <summary>Metadata describing a pull request on the repository host.</summary>
public sealed record PullRequest(
    int Id,
    string Title,
    string? Description,
    string SourceBranch,
    string TargetBranch,
    string Author);

/// <summary>
/// A single file changed in a pull request, with its unified diff and — for supported
/// source files — the full post-change content used for semantic analysis.
/// </summary>
public sealed record ChangedFile(
    string Path,
    string? OldPath,
    ChangeType ChangeType,
    string Diff,
    string? NewContent);

/// <summary>
/// A comment to publish on a pull request. <see cref="Line"/> &gt; 0 with a non-null
/// <see cref="FilePath"/> produces an inline comment; otherwise a general PR comment.
/// </summary>
public sealed record ReviewComment(string? FilePath, int Line, string Markdown);
