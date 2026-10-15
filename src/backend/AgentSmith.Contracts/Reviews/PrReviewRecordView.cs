namespace AgentSmith.Contracts.Reviews;

/// <summary>2026-10-13-9a59b: one pull request's stored record.</summary>
/// <param name="PrKey"><see cref="Reviews.PrKey"/> of the URL.</param>
/// <param name="Url">The readable URL.</param>
/// <param name="State">One of <see cref="PrReviewStates"/>.</param>
/// <param name="StateAt">When the host says the state began; null before any read.</param>
/// <param name="RunId">The newest code run that names the pull request.</param>
/// <param name="LastEntryAt">The newest entry's time, derived on read.</param>
public sealed record PrReviewRecordView(
    string PrKey,
    string Url,
    string Repo,
    string Number,
    string Project,
    string TicketId,
    string RunId,
    string State,
    DateTimeOffset? StateAt,
    DateTimeOffset CheckedAt,
    DateTimeOffset? LastEntryAt,
    IReadOnlyList<PrReviewEntryView> Entries)
{
    /// <summary>2026-10-15-114fb: the newest entry that is not ours, not a bot's, and not a host system note unless
    /// it carries a decision — a person's activity; derived on read, null when there is none.</summary>
    public DateTimeOffset? LastPersonEntryAt { get; init; }

    /// <summary>2026-10-15-114fc: the head commit the newest successful read saw.</summary>
    public string? HeadSha { get; init; }

    /// <summary>2026-10-15-114fc: when a read saw the head change; null until one did.</summary>
    public DateTimeOffset? HeadMovedAt { get; init; }

    /// <summary>2026-10-15-114fc: when a successful read saw the merged or closed pull request open again.</summary>
    public DateTimeOffset? ReopenedAt { get; init; }
}
