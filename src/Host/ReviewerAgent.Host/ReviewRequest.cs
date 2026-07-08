namespace ReviewerAgent.Host;

/// <summary>The pull request to review for this run.</summary>
public sealed record ReviewRequest(int PullRequestId);
