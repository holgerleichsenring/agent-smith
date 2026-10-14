using AgentSmith.Server.Services.Tickets.Defaults;

namespace AgentSmith.Server.Services.Tickets.Runway;

/// <summary>
/// 2026-10-13-8762v: how many pending defaults marked holds_series wait on a ticket — its runs' and its bound
/// conversation's, as the ticket page counts them. A verdict queues a nudge, so a ratify reaches the worker again.
/// </summary>
public sealed class SeriesHolds(TicketDefaultsReader defaults)
{
    public async Task<int> PendingAsync(string project, string ticketId, CancellationToken ct) =>
        (await defaults.ReadAsync(project, ticketId, mayReadTranscript: false, ct))?.PendingHoldingSeries ?? 0;
}
