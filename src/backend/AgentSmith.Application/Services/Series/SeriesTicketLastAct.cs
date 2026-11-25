using AgentSmith.Contracts.Series;

namespace AgentSmith.Application.Services.Series;

/// <summary>2026-11-25-9992e: the series master's newest act on one ticket, from the act log — what it did, why, and when.</summary>
public sealed record SeriesTicketLastAct(string Text, string? Reason, DateTimeOffset At)
{
    /// <summary>The newest act of <paramref name="acts"/> naming <paramref name="ticketId"/>; null when the master never acted on it.</summary>
    public static SeriesTicketLastAct? Of(IReadOnlyList<SeriesAct> acts, string ticketId)
    {
        ArgumentNullException.ThrowIfNull(acts);
        return acts.LastOrDefault(a => string.Equals(a.Ticket, ticketId, StringComparison.Ordinal)) is { } act
            ? new SeriesTicketLastAct(act.Text, act.Reason, act.At) : null;
    }
}
