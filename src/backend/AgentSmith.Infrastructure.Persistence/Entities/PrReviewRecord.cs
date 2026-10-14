namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// 2026-10-13-9a59b: one pull request a ticket's code runs named — keyed by the hash of its URL as the
/// run stored it, never by a run, because one pull request spans attempts. The state lives here
/// only; comments, review bodies and decisions are its <see cref="PrReviewEntry"/> rows. Times are UTC
/// ticks: SQLite cannot compare a DateTimeOffset, and the refresh's compare-and-set on
/// <see cref="CheckedAtTicks"/> runs in SQL.
/// </summary>
public sealed class PrReviewRecord : EntityBase
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
}
