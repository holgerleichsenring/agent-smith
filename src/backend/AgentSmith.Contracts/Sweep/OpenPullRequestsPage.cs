namespace AgentSmith.Contracts.Sweep;

/// <summary>2026-10-08-10b0: a budgeted read of a repository's open pull requests; a cut read
/// resumes at <see cref="Resume"/>.</summary>
public sealed record OpenPullRequestsPage(IReadOnlyList<OpenPullRequest> Items, bool Cut = false, string? Resume = null);
