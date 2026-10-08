namespace AgentSmith.Contracts.Runs;

/// <summary>
/// 2026-10-08-7c0e: the ticket's newest code attempt before the current run — the Run row, not a
/// guess. <see cref="StartedAt"/> is the server's clock at launch; a resumed row carries its resume
/// time, so "since the previous attempt" means since it last ran. <see cref="Finished"/> is true once a
/// terminal status landed (success, shortfall, failed, cancelled).
/// </summary>
public sealed record PreviousAttempt(string RunId, string Status, DateTimeOffset StartedAt, bool Finished)
{
    /// <summary>2026-10-08-e8b9d: each repo's latest opened pull-request URL across the ticket's code
    /// runs — the pull requests a rework reads its review from.</summary>
    public IReadOnlyDictionary<string, string> PullRequestUrls { get; init; } = new Dictionary<string, string>();

    /// <summary>2026-10-08-0781: when the run read the ticket and its pull requests — taken before the
    /// reads, kept across a resume. Null on a run that never read them (or an older row).</summary>
    public DateTimeOffset? ActsReadAt { get; init; }

    /// <summary>2026-10-08-0781: what the run had seen: its read, else its start.</summary>
    public DateTimeOffset Cutoff => ActsReadAt ?? StartedAt;

    /// <summary>The skew allowed between a host's clock and the server's. 2026-10-08-0781: it is
    /// SUBTRACTED, so an act near the cutoff is served again rather than lost.</summary>
    public static readonly TimeSpan SkewMargin = TimeSpan.FromSeconds(5);

    /// <summary>True when <paramref name="at"/> may be after what this attempt read.</summary>
    public bool Precedes(DateTimeOffset at) => at > Cutoff - SkewMargin;
}
