using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using ReviewerAgent.CommentGenerator.Contracts;
using ReviewerAgent.RepoConnector.Contracts;
using ReviewerAgent.Reviewer.Contracts;

namespace ReviewerAgent.CommentGenerator;

/// <summary>
/// Formats review findings as Markdown PR comments and publishes them via the repo
/// connector, deduplicating within the run and isolating per-comment failures.
/// </summary>
internal sealed class CommentGenerator(IRepoConnector repo, ILogger<CommentGenerator> logger) : ICommentGenerator
{
    private readonly IRepoConnector _repo = repo;
    private readonly ILogger<CommentGenerator> _logger = logger;

    public async Task PublishAsync(int prId, ReviewResult result, CancellationToken cancellationToken = default)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        int published = 0, skipped = 0, failures = 0;

        foreach (var finding in result.Findings)
        {
            var key = Hash($"{finding.File}|{finding.Line}|{finding.Title}");
            if (!seen.Add(key))
            {
                skipped++;
                _logger.LogDebug("Skipping duplicate finding '{Title}' on {File}:{Line}.",
                    finding.Title, finding.File, finding.Line);
                continue;
            }

            var comment = new ReviewComment(finding.File, finding.Line, FormatMarkdown(finding));
            try
            {
                await _repo.PostCommentAsync(prId, comment, cancellationToken);
                published++;
                _logger.LogDebug("Published finding '{Title}' on {File}:{Line}.",
                    finding.Title, finding.File, finding.Line);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failures++;
                _logger.LogError(ex, "Failed to publish finding '{Title}' on {File}.", finding.Title, finding.File);
            }
        }

        _logger.LogInformation("Published {Published} comments, skipped {Skipped} duplicates, {Failures} failures.",
            published, skipped, failures);
    }

    private static string FormatMarkdown(Finding finding)
    {
        var sb = new StringBuilder();
        sb.Append("**[").Append(finding.Severity).Append("]** `").Append(finding.Category).Append('`');
        sb.Append(" — ").AppendLine(finding.Title);
        sb.AppendLine();
        sb.AppendLine(finding.Comment);
        if (!string.IsNullOrWhiteSpace(finding.Recommendation))
        {
            sb.AppendLine();
            sb.Append("**Recommendation:** ").AppendLine(finding.Recommendation);
        }

        return sb.ToString();
    }

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }
}
