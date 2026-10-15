namespace AgentSmith.Contracts.Reviews;

/// <summary>2026-10-13-9a59b: what the host says about a pull request now.</summary>
/// <param name="Number">The pull request's number on its host, as the PR sweep keys it.</param>
/// <param name="State">One of <see cref="PrReviewStates"/> open, merged, closed.</param>
/// <param name="StateAt">When it was merged or closed; for an open one, when it was opened.</param>
/// <param name="Decisions">The decisions standing now, at most one per reviewer.</param>
/// <param name="HeadSha">The pull request's head commit, when the read fetched it.</param>
public sealed record PrReviewState(
    string Number, string State, DateTimeOffset StateAt, IReadOnlyList<PrReviewDecision> Decisions, string? HeadSha = null);
