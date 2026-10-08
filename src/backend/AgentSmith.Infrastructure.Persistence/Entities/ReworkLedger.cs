namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// 2026-10-08-0781: what the rework worker must remember about a ticket beyond any one nudge —
/// the acts an operator's stop withholds (<see cref="NotServedThroughTicks"/>) and every text it
/// already said, so a crash or a second nudge never says it twice.
/// </summary>
public sealed class ReworkLedger : EntityBase
{
    public string Project { get; set; } = string.Empty;
    public string TicketId { get; set; } = string.Empty;
    public long? NotServedThroughTicks { get; set; }

    /// <summary>The spoken keys, one per line, newest last; the oldest drop beyond a cap.</summary>
    public string SpokenKeys { get; set; } = string.Empty;
}
