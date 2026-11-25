namespace AgentSmith.Contracts.Series;

/// <summary>
/// 2026-11-25-9992g: one event on a member ticket the series master has not judged yet, kept on the series record until a
/// beat hands it over. <see cref="At"/> and <see cref="TicketId"/> are stamped where it is recorded
/// (<see cref="ISeriesWakes"/>), so the beat clears exactly the wakes up to the newest one it handed over.
/// </summary>
/// <param name="Kind">One of <see cref="SeriesWakeKinds"/>.</param>
/// <param name="Text">What happened, in words; a comment's own text.</param>
/// <param name="Author">Who wrote a comment; null for an event nobody wrote.</param>
public sealed record SeriesWake(string Kind, string Text, string? Author = null)
{
    public DateTimeOffset At { get; init; }

    public string TicketId { get; init; } = string.Empty;

    /// <summary>The pull request a comment was written on; null for a comment on the ticket and for other events.</summary>
    public string? PrUrl { get; init; }

    /// <summary>The same event, whenever it was recorded — a webhook delivered twice wakes once.</summary>
    public bool SameAs(SeriesWake other) => other is not null && this with { At = other.At } == other;
}
