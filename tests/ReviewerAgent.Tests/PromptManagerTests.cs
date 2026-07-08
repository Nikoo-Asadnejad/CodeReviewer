using ReviewerAgent.CodeScanner.Contracts;
using ReviewerAgent.ContextBuilder.Contracts;
using ReviewerAgent.RepoConnector.Contracts;
using ReviewerAgent.Rules.Contracts;
using ReviewerAgent.TaskConnector.Contracts;
using ReviewerAgent.TaskManager.Contracts;
using Xunit;

namespace ReviewerAgent.Tests;

public class PromptManagerTests
{
    [Fact]
    public void Build_includes_files_rules_task_and_schema()
    {
        var pr = new PullRequest(1, "Add feature", "Implements AB#123", "feature", "main", "me");
        var file = new ScannedFile(
            new ChangedFile("src/X.cs", null, ChangeType.Modified, "", "class X {}"),
            SemanticContext.Empty);
        var context = new ReviewContext(
            new CodeScanResult(pr, [file]),
            new TaskUnderstanding(1, "123", true, new WorkItem("123", "Title", "Desc", "MUST-WORK", "Active")));
        var rules = new[] { new RuleDocument("Security", "SEC-RULE-CONTENT") };

        var prompt = new ReviewerAgent.PromptManager.PromptManager().Build(context, rules);

        Assert.Contains("src/X.cs", prompt.User);
        Assert.Contains("SEC-RULE-CONTENT", prompt.User);
        Assert.Contains("MUST-WORK", prompt.User);
        Assert.Contains("Output Format", prompt.User);
        Assert.False(string.IsNullOrWhiteSpace(prompt.System));
    }
}
