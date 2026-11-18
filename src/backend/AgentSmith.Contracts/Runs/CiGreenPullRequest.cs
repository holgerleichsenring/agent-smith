namespace AgentSmith.Contracts.Runs;

/// <summary>2026-11-17-3435b: one repository's pull request a merge-green run merges.</summary>
public sealed record CiGreenPullRequest(string Repo, string Url);
