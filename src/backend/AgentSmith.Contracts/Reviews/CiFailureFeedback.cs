namespace AgentSmith.Contracts.Reviews;

/// <summary>2026-11-17-3435c: one pull request's red CI as a fix run reads it — its repository, its URL, the failed checks.</summary>
public sealed record CiFailureFeedback(string Repo, string PrUrl, IReadOnlyList<CiFailure> Failures);
