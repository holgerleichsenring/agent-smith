namespace AgentSmith.Application.Services.Series;

/// <summary>2026-11-23-31dcb: a member ticket's pull request in one repo, from the PR review records — the open one, else
/// the newest — with its CI on its head (one of CiStates; null before a CI read on it) and a red head's first failed check
/// with its log link.</summary>
/// <param name="Target">The branch it targets when known (2026-11-24-f9c5c: read for an open record too, per head).</param>
public sealed record SeriesRunPullRequestView(
    string Repo, string Number, string Url, string State, string? Ci, string? CiFailure, string? CiLog, string? Target)
{
    /// <summary>2026-11-23-8d16: the newest read saw it still a draft; a person's merge takes it out of draft first.</summary>
    public bool IsDraft { get; init; }

    /// <summary>2026-11-24-6811a: the host said last that it conflicts with its base.</summary>
    public bool HasConflicts { get; init; }

    /// <summary>2026-11-24-6811d: the refresh-base run at work on it; null when none is.</summary>
    public string? RefreshRunId { get; init; }

    /// <summary>2026-11-23-0219: the newest merge-green run's refusal or armed auto-complete on it; null when none said one.</summary>
    public AgentSmith.Contracts.Runs.PullRequestMergeSaid? MergeSaid { get; init; }

    /// <summary>2026-11-25-9992i: who approves it now on the host (a standing approval, ours left out) — under review: each the
    /// series master merges once a person did.</summary>
    public IReadOnlyList<string> ApprovedBy { get; init; } = [];

    /// <summary>2026-11-26-5201f: the host's merge rules still need a person's approval on it — the host's rule, apart from the
    /// series' own review mode, which <see cref="ApprovedBy"/> serves.</summary>
    public bool ApprovalRequired { get; init; }

    /// <summary>2026-11-27-ecf2b: the run that named it said it carries only the spec record (spec, done index, decisions).</summary>
    public bool RecordOnly { get; init; }

    /// <summary>The pull request waits for a merge: open, or opened and not read by the host yet.</summary>
    public static bool Waits(SeriesRunPullRequestView pullRequest)
    {
        ArgumentNullException.ThrowIfNull(pullRequest);
        return pullRequest.State is AgentSmith.Contracts.Reviews.PrReviewStates.Open or AgentSmith.Contracts.Reviews.PrReviewStates.Unread;
    }
}
