namespace ReviewerAgent.CodeScanner.Contracts;

/// <summary>
/// Scans a pull request: retrieves its changed files via the repo connector and
/// extracts semantic context with Roslyn.
/// </summary>
public interface ICodeScanner
{
    Task<CodeScanResult> ScanAsync(int prId, CancellationToken cancellationToken = default);
}
