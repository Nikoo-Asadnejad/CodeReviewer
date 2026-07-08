using ReviewerAgent.CodeScanner.Contracts;
using ReviewerAgent.ContextBuilder.Contracts;
using ReviewerAgent.PromptManager.Contracts;
using ReviewerAgent.Rules.Contracts;
using System.Text;

namespace ReviewerAgent.PromptManager;

/// <summary>Builds the system + user prompt from the review context and rule documents.</summary>
internal sealed class PromptManager : IPromptManager
{
    private const string SystemPrompt =
        "You are a Staff Software Engineer and senior .NET code reviewer. Review ONLY the submitted pull-request changes. " +
        "Use the provided semantic context and the linked task's acceptance criteria to judge whether the " +
        "changes are correct and complete. Do not speculate about code you cannot see; if context is " +
        "insufficient to judge something, say so rather than guessing. Be specific and actionable. " +
        "Make sure all the porject rules are applied and not violated" +
        "Respond with a single JSON object and nothing else.";

    public ReviewPrompt Build(ReviewContext context, IReadOnlyList<RuleDocument> rules)
    {
        var sb = new StringBuilder();

        var pr = context.PullRequest;
        sb.AppendLine("# Pull Request");
        sb.AppendLine($"Title: {pr.Title}");
        sb.AppendLine($"Author: {pr.Author}");
        sb.AppendLine($"Source -> Target: {pr.SourceBranch} -> {pr.TargetBranch}");
        if (!string.IsNullOrWhiteSpace(pr.Description))
        {
            sb.AppendLine($"Description: {pr.Description}");
        }

        sb.AppendLine();
        sb.AppendLine("# Task / Acceptance Criteria");
        AppendTask(sb, context.Task);

        sb.AppendLine();
        sb.AppendLine("# Changed Files");
        foreach (var file in context.Code.Files)
        {
            AppendFile(sb, file);
        }

        sb.AppendLine();
        sb.AppendLine("# Review Rules");
        foreach (var rule in rules)
        {
            sb.AppendLine($"## {rule.Name}");
            sb.AppendLine(rule.Content);
            sb.AppendLine();
        }

        sb.AppendLine(JsonSchemaInstruction);

        return new ReviewPrompt(SystemPrompt, sb.ToString());
    }

    private static void AppendTask(StringBuilder sb, ReviewerAgent.TaskManager.Contracts.TaskUnderstanding task)
    {
        if (!task.Found || task.WorkItem is null)
        {
            sb.AppendLine(task.TaskId is null
                ? "No task was linked in the PR description."
                : $"Task '{task.TaskId}' was referenced but could not be retrieved.");
            return;
        }

        var wi = task.WorkItem;
        sb.AppendLine($"Task {wi.Id}: {wi.Title} (state: {wi.State ?? "unknown"})");
        if (!string.IsNullOrWhiteSpace(wi.Description))
        {
            sb.AppendLine($"Description: {wi.Description}");
        }

        if (!string.IsNullOrWhiteSpace(wi.AcceptanceCriteria))
        {
            sb.AppendLine($"Acceptance Criteria: {wi.AcceptanceCriteria}");
        }
    }

    private static void AppendFile(StringBuilder sb, ScannedFile scanned)
    {
        var file = scanned.File;
        sb.AppendLine($"## {file.Path} [{file.ChangeType}]");
        if (file.ChangeType == ReviewerAgent.RepoConnector.Contracts.ChangeType.Renamed && file.OldPath is not null)
        {
            sb.AppendLine($"(renamed from {file.OldPath})");
        }

        var ctx = scanned.Context;
        if (!ctx.IsEmpty)
        {
            sb.AppendLine("Semantic context:");
            if (ctx.Namespace is not null)
            {
                sb.AppendLine($"- namespace: {ctx.Namespace}");
            }

            if (ctx.Types.Count > 0)
            {
                sb.AppendLine($"- types: {string.Join("; ", ctx.Types)}");
            }

            if (ctx.Interfaces.Count > 0)
            {
                sb.AppendLine($"- interfaces: {string.Join(", ", ctx.Interfaces)}");
            }

            if (ctx.Methods.Count > 0)
            {
                sb.AppendLine($"- methods: {string.Join("; ", ctx.Methods)}");
            }

            if (ctx.References.Count > 0)
            {
                sb.AppendLine($"- references: {string.Join(", ", ctx.References)}");
            }

            if (!string.IsNullOrWhiteSpace(ctx.XmlDocumentation))
            {
                sb.AppendLine($"- docs: {ctx.XmlDocumentation}");
            }
        }

        if (!string.IsNullOrWhiteSpace(file.NewContent))
        {
            sb.AppendLine("Changed content:");
            sb.AppendLine("```csharp");
            sb.AppendLine(file.NewContent);
            sb.AppendLine("```");
        }
        else if (!string.IsNullOrWhiteSpace(file.Diff))
        {
            sb.AppendLine("Diff:");
            sb.AppendLine("```diff");
            sb.AppendLine(file.Diff);
            sb.AppendLine("```");
        }

        sb.AppendLine();
    }

    private const string JsonSchemaInstruction = """
        # Output Format
        Return a single JSON object exactly matching this schema (no markdown, no prose outside the JSON):
        {
          "summary": "string - overall assessment of the change",
          "findings": [
            {
              "title": "string",
              "severity": "Critical | High | Medium | Low | Suggestion",
              "category": "string - e.g. Architecture, Security, Performance, CleanCode, Testing, Naming, Business",
              "file": "string - file path",
              "line": 0,
              "comment": "string - what is wrong and why",
              "recommendation": "string - how to fix it",
              "confidence": 0.0
            }
          ]
        }
        If there are no issues, return an empty "findings" array.
        """;
}
