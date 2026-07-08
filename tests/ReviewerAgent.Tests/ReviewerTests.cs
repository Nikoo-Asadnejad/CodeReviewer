using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReviewerAgent.CodeScanner.Contracts;
using ReviewerAgent.ContextBuilder.Contracts;
using ReviewerAgent.LlmConnector.Contracts;
using ReviewerAgent.PromptManager.Contracts;
using ReviewerAgent.RepoConnector.Contracts;
using ReviewerAgent.Reviewer;
using ReviewerAgent.Reviewer.Contracts;
using ReviewerAgent.TaskManager.Contracts;
using Xunit;

namespace ReviewerAgent.Tests;

public class ReviewerTests
{
    private const string ValidJson =
        """{"summary":"ok","findings":[{"title":"Bug","severity":"High","category":"Security","file":"a.cs","line":12,"comment":"c","recommendation":"r","confidence":0.9}]}""";

    [Fact]
    public void StripCodeFences_removes_json_fence()
    {
        var fenced = "```json\n{\"a\":1}\n```";
        Assert.Equal("{\"a\":1}", AiReviewer.StripCodeFences(fenced));
    }

    [Fact]
    public void StripCodeFences_returns_plain_json_unchanged()
    {
        Assert.Equal("{\"a\":1}", AiReviewer.StripCodeFences("  {\"a\":1}  "));
    }

    [Fact]
    public async Task ReviewAsync_parses_valid_json()
    {
        var result = await Review(ValidJson);
        Assert.Single(result.Findings);
        Assert.Equal(Severity.High, result.Findings[0].Severity);
        Assert.Equal("Security", result.Findings[0].Category);
    }

    [Fact]
    public async Task ReviewAsync_parses_code_fenced_json()
    {
        var result = await Review("```json\n" + ValidJson + "\n```");
        Assert.Single(result.Findings);
    }

    [Fact]
    public async Task ReviewAsync_returns_empty_on_malformed_json()
    {
        var result = await Review("not json at all");
        Assert.Empty(result.Findings);
    }

    private static async Task<ReviewResult> Review(string llmText)
    {
        var llm = new Mock<ILlmConnector>();
        llm.Setup(c => c.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new LlmResponse(llmText));

        var reviewer = new AiReviewer(llm.Object, NullLogger<AiReviewer>.Instance);
        return await reviewer.ReviewAsync(new ReviewPrompt("sys", "usr"), SampleContext());
    }

    private static ReviewContext SampleContext()
    {
        var pr = new PullRequest(1, "t", null, "feature", "main", "me");
        var code = new CodeScanResult(pr, []);
        return new ReviewContext(code, TaskUnderstanding.NotFound(1));
    }
}
