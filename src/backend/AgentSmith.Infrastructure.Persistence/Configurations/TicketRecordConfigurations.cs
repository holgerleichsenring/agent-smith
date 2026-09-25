using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>
/// 2026-09-25-b4d9: the tables keyed by a TICKET rather than by a run — what is queued for
/// one, what spec set belongs to it, what a run could not move, and that this framework took
/// it up and owes it a run. Lifted out of the context like RunChildConfiguration before it,
/// which was at its length ceiling when this phase added the fourth of them.
/// </summary>
public sealed class TicketRecordConfigurations
{
    public void Apply(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfiguration(new QueuedTicketConfiguration());
        modelBuilder.ApplyConfiguration(new TicketSpecSetConfiguration()); // p0390
        modelBuilder.ApplyConfiguration(new UnmovedTicketConfiguration()); // 2026-09-18-c1a7
        modelBuilder.ApplyConfiguration(new TakenTicketConfiguration()); // 2026-09-25-b4d9
    }
}
