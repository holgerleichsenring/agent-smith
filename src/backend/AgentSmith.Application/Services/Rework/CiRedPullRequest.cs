using AgentSmith.Contracts.Reviews;

namespace AgentSmith.Application.Services.Rework;

/// <summary>2026-11-17-3435c: one pull request red on its head, with the failed checks the record stored.</summary>
public sealed record CiRedPullRequest(string Repo, string Url, IReadOnlyList<CiFailure> Failures);
