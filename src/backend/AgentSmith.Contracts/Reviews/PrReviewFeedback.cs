namespace AgentSmith.Contracts.Reviews;

/// <summary>2026-10-08-e8b9d: the review read from one open pull request of the previous attempt.</summary>
public sealed record PrReviewFeedback(string Repo, string PrUrl, IReadOnlyList<PrReviewThread> Threads);
