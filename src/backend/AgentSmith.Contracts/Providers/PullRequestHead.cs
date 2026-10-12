namespace AgentSmith.Contracts.Providers;

/// <summary>A pull request's head branch, whether it lives in the same repository (no fork), and its URL.</summary>
public sealed record PullRequestHead(string? HeadRef, bool SameRepository, string Url);
