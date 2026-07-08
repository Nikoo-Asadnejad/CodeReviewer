using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReviewerAgent.Rules.Contracts;

namespace ReviewerAgent.Rules;

/// <summary>Loads review rule documents from *.md files in the configured folder.</summary>
internal sealed class MarkdownRuleProvider(IOptions<RulesOptions> options, ILogger<MarkdownRuleProvider> logger) : IRuleProvider
{
    private readonly RulesOptions _options = options.Value;
    private readonly ILogger<MarkdownRuleProvider> _logger = logger;

    public async Task<IReadOnlyList<RuleDocument>> LoadRulesAsync(CancellationToken cancellationToken = default)
    {
        var folder = _options.Folder;
        if (!Directory.Exists(folder))
        {
            throw new InvalidOperationException($"Rules folder not found: {Path.GetFullPath(folder)}");
        }

        var paths = Directory.GetFiles(folder, "*.md", SearchOption.TopDirectoryOnly);
        if (paths.Length == 0)
        {
            _logger.LogWarning("No rule documents (*.md) found in {Folder}.", Path.GetFullPath(folder));
            return [];
        }

        var documents = new List<RuleDocument>(paths.Length);
        foreach (var path in paths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            var content = await File.ReadAllTextAsync(path, cancellationToken);
            documents.Add(new RuleDocument(Path.GetFileNameWithoutExtension(path), content));
        }

        _logger.LogInformation("Loaded {Count} rule documents: {Names}",
            documents.Count, string.Join(", ", documents.Select(d => d.Name)));

        return documents;
    }
}
