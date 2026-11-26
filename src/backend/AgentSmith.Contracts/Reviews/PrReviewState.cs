namespace AgentSmith.Contracts.Reviews;

/// <summary>2026-10-13-9a59b: what the host says about a pull request now.</summary>
/// <param name="Number">The pull request's number on its host, as the PR sweep keys it.</param>
/// <param name="State">One of <see cref="PrReviewStates"/> open, merged, closed.</param>
/// <param name="StateAt">When it was merged or closed; for an open one, when it was opened.</param>
/// <param name="Decisions">The decisions standing now, at most one per reviewer.</param>
/// <param name="HeadSha">The pull request's head commit, when the read fetched it.</param>
/// <param name="MergeSha">2026-11-23-8df6a: the commit a merge landed as; null unless the state is merged and the host named one.</param>
public sealed record PrReviewState(
    string Number, string State, DateTimeOffset StateAt, IReadOnlyList<PrReviewDecision> Decisions, string? HeadSha = null,
    string? MergeSha = null)
{
    /// <summary>2026-11-23-8d16: the host says the pull request is still a draft — Azure DevOps cannot complete one.</summary>
    public bool IsDraft { get; init; }

    /// <summary>2026-11-24-6811a: the host says the pull request conflicts with its base (true), merges cleanly (false), or
    /// has not decided yet — a host computes mergeability after the base moves (null).</summary>
    public bool? HasConflicts { get; init; }

    /// <summary>2026-11-24-629ab: the host says auto-complete is set on the pull request (true) or not (false); null when
    /// its state read does not say — GitHub (2026-11-25-7a92c: GitLab says it, as merge_when_pipeline_succeeds).</summary>
    public bool? AutoCompleteArmed { get; init; }

    /// <summary>2026-11-26-5201f: the host's merge rules still need a person's approval on the open pull request (true) — an
    /// Azure reviewer policy, a GitHub required review, a GitLab approval rule — or do not (false); null when the read could not
    /// tell. A person's step, never CI.</summary>
    public bool? ApprovalRequired { get; init; }
}
