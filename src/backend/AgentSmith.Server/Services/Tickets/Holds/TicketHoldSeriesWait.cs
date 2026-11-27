namespace AgentSmith.Server.Services.Tickets.Holds;

/// <summary>2026-11-26-f511a: what a ticket of an active series waits on — the keys of its requirements that have not landed —
/// and when its master last took it up (<see cref="AgentSmith.Application.Services.Holds.TicketSeriesWait"/>).</summary>
/// <param name="StartRefusedAt">2026-11-27-ecf2e: when its master was refused starting it (TicketSeriesWait.StartRefusedAt).</param>
public sealed record TicketHoldSeriesWait(IReadOnlyList<string> Unlanded, DateTimeOffset? TakenUpAt, DateTimeOffset? StartRefusedAt = null)
{
    public static TicketHoldSeriesWait None { get; } = new([], null);
}
