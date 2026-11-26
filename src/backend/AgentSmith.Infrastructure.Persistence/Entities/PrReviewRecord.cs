namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// 2026-10-13-9a59b: one pull request a ticket's code runs named — keyed by the hash of its URL as the
/// run stored it, never by a run, because one pull request spans attempts. The state lives here
/// only; comments, review bodies and decisions are its <see cref="PrReviewEntry"/> rows. Times are UTC
/// ticks: SQLite cannot compare a DateTimeOffset, and the refresh's compare-and-set on
/// <see cref="CheckedAtTicks"/> runs in SQL.
/// </summary>
public sealed class PrReviewRecord : EntityBase, IProjectOwned
{
    public long Id { get; set; }

    /// <summary>PrKey.Of the URL exactly as Runs.PullRequestsJson stores it.</summary>
    public string PrKey { get; set; } = string.Empty;

    /// <summary>The readable URL; unindexed.</summary>
    public string Url { get; set; } = string.Empty;

    public string Repo { get; set; } = string.Empty;

    /// <summary>RepoKey.Of the configured repository URL — the key the PR sweep uses.</summary>
    public string RepoKey { get; set; } = string.Empty;

    /// <summary>The number on the host, as the PR sweep stores it.</summary>
    public string Number { get; set; } = string.Empty;

    public string Project { get; set; } = string.Empty;
    public string TicketId { get; set; } = string.Empty;

    /// <summary>The newest code run naming the pull request; the record dies with it.</summary>
    public string RunId { get; set; } = string.Empty;

    /// <summary>open, merged, closed, unsupported or unread.</summary>
    public string State { get; set; } = string.Empty;

    public long? StateAtTicks { get; set; }
    public long CheckedAtTicks { get; set; }

    /// <summary>2026-10-15-114fc: the head commit the newest successful read saw; null before a host reported one.</summary>
    public string? HeadSha { get; set; }

    /// <summary>The check time of the read that saw the stored head change; null until one did.</summary>
    public long? HeadMovedAtTicks { get; set; }

    /// <summary>The check time of the successful read that saw a merged or closed record open again.</summary>
    public long? ReopenedAtTicks { get; set; }

    /// <summary>2026-11-17-3435a: none, pending, green or red for <see cref="CiHeadSha"/>; null before a CI read.</summary>
    public string? CiState { get; set; }

    /// <summary>The head the CI state was read for.</summary>
    public string? CiHeadSha { get; set; }

    /// <summary>The check time of the read that first saw this CI state on this head.</summary>
    public long? CiAtTicks { get; set; }

    /// <summary>A red head's failed checks (name, link, bounded excerpt) as a JSON array; null otherwise.</summary>
    public string? CiFailuresJson { get; set; }

    /// <summary>2026-11-17-3435b: '&lt;head&gt;:&lt;state&gt;' the CI loop last acted on (a merge started, a fix asked for);
    /// null before it acted. A new head or a new verdict on the same head is acted on again.</summary>
    public string? CiActed { get; set; }

    /// <summary>2026-11-22-6d85b: the branch a merged pull request merged into, read once after the merge; null before
    /// one was read (an open record, an older row, a host that did not say).</summary>
    public string? TargetBranch { get; set; }

    /// <summary>2026-11-23-8d16: the newest successful read saw the pull request still a draft.</summary>
    public bool IsDraft { get; set; }

    /// <summary>2026-11-23-8df6a: the commit a merged pull request landed as, from every merged state read; null before a read
    /// named one (an open record, a merge no read saw since the upgrade).</summary>
    public string? MergeSha { get; set; }

    /// <summary>2026-11-24-6811a: the host says the open pull request conflicts with its base; null before a host decided.</summary>
    public bool? HasConflicts { get; set; }

    /// <summary>2026-11-24-6811a: the check time of the read that first saw the conflict; null while it does not conflict.</summary>
    public long? ConflictsAtTicks { get; set; }

    /// <summary>2026-11-24-629ab: the newest read saw auto-complete set (true) or not (false); null when the host does not say.</summary>
    public bool? AutoCompleteArmed { get; set; }

    /// <summary>2026-11-24-629ab: the check time of the read that found auto-complete gone while the pull request conflicted
    /// (then or at the read before); null when nothing was lost, it is armed again, or a person took it off cleanly.</summary>
    public long? AutoCompleteLostAtTicks { get; set; }

    /// <summary>2026-11-24-629ab: the head the CI loop armed auto-complete again on; written apart from a read.</summary>
    public string? AutoCompleteRearmedHead { get; set; }

    /// <summary>2026-11-25-098f: the moved head auto-complete was found gone on that no run had recorded pushing yet — asked about
    /// again at every read until a run records it (then lost) or it is set again; null otherwise. A person's head stays here.</summary>
    public string? AutoCompleteGoneHead { get; set; }

    /// <summary>2026-11-26-5201f: the host said last that the open pull request still needs a person's approval (true) or not
    /// (false); null before a read could tell, after a head move no read decided, and for an ended one.</summary>
    public bool? ApprovalRequired { get; set; }
}
