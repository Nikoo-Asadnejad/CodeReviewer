using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReviewerAgent.CodeScanner.Contracts;
using ReviewerAgent.RepoConnector.Contracts;

namespace ReviewerAgent.CodeScanner;

/// <summary>
/// Retrieves the PR's changed files via the repo connector and extracts lightweight
/// semantic context from changed C# files using Roslyn.
/// </summary>
internal sealed class RoslynCodeScanner(IRepoConnector repo, IOptions<CodeScannerOptions> options, ILogger<RoslynCodeScanner> logger) : ICodeScanner
{
    private readonly IRepoConnector _repo = repo;
    private readonly CodeScannerOptions _options = options.Value;
    private readonly ILogger<RoslynCodeScanner> _logger = logger;

    public async Task<CodeScanResult> ScanAsync(int prId, CancellationToken cancellationToken = default)
    {
        var pr = await _repo.GetPullRequestAsync(prId, cancellationToken);
        var changed = await _repo.GetChangedFilesAsync(prId, cancellationToken);

        var reviewable = changed
            .Where(f => f.ChangeType != ChangeType.Deleted)
            .Where(f => !IsIgnored(f.Path))
            .Take(_options.MaxFilesToAnalyze)
            .ToList();

        _logger.LogInformation("CodeScanner: {Count} of {Total} changed files selected for analysis.",
            reviewable.Count, changed.Count);

        var scanned = new List<ScannedFile>(reviewable.Count);
        foreach (var file in reviewable)
        {
            scanned.Add(new ScannedFile(file, BuildContext(file)));
        }

        return new CodeScanResult(pr, scanned);
    }

    private bool IsIgnored(string path) =>
        _options.IgnoredFilePatterns.Any(p =>
            !string.IsNullOrWhiteSpace(p) && path.EndsWith(p, StringComparison.OrdinalIgnoreCase));

    private SemanticContext BuildContext(ChangedFile file)
    {
        if (!file.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(file.NewContent))
        {
            _logger.LogDebug("Skipping semantic analysis for non-C# or empty file {Path}.", file.Path);
            return SemanticContext.Empty;
        }

        try
        {
            var root = CSharpSyntaxTree.ParseText(file.NewContent).GetRoot();

            var ns = root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>()
                .FirstOrDefault()?.Name.ToString();

            var typeDecls = root.DescendantNodes().OfType<TypeDeclarationSyntax>().ToList();

            var types = typeDecls
                .Select(t => $"{t.Keyword.Text} {t.Identifier.Text}{t.BaseList}".Trim())
                .ToList();

            var interfaces = typeDecls.OfType<InterfaceDeclarationSyntax>()
                .Select(t => t.Identifier.Text)
                .ToList();

            var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
                .Select(m => $"{m.ReturnType} {m.Identifier.Text}{m.ParameterList}")
                .Take(_options.MaxMethodsPerFile)
                .ToList();

            var references = root.DescendantNodes().OfType<UsingDirectiveSyntax>()
                .Select(u => u.Name?.ToString())
                .Where(n => !string.IsNullOrEmpty(n))
                .Select(n => n!)
                .Distinct()
                .ToList();

            var xmlDoc = ExtractXmlDoc(typeDecls, _options.MaxContextSizeChars);

            return new SemanticContext(ns, types, methods, interfaces, references, xmlDoc);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Roslyn analysis failed for {Path}; continuing with empty context.", file.Path);
            return SemanticContext.Empty;
        }
    }

    private string? ExtractXmlDoc(IEnumerable<TypeDeclarationSyntax> types, int maxChars)
    {
        var docs = types
            .SelectMany(t => t.GetLeadingTrivia())
            .Where(tr => tr.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                         tr.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
            .Select(tr => tr.ToFullString().Trim());

        var combined = string.Join("\n", docs).Trim();
        if (combined.Length == 0)
        {
            return null;
        }

        if (combined.Length > maxChars)
        {
            _logger.LogWarning("Trimming XML documentation from {Original} to {Max} chars.", combined.Length, maxChars);
            combined = combined[..maxChars];
        }

        return combined;
    }
}
