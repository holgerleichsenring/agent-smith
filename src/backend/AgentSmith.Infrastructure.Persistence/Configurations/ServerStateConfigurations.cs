using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>
/// 2026-10-02-5ab2a: the tables a server keeps ABOUT ITSELF — the callers it has seen and the
/// state it must not lose when Redis is flushed. Grouped like TicketRecordConfigurations, so
/// the context stays at its length limit as later state tables join.
/// </summary>
public sealed class ServerStateConfigurations
{
    public void Apply(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfiguration(new ObservedCallerConfiguration()); // 2026-08-26-7a51
        modelBuilder.ApplyConfiguration(new ConnectionDiscoveryConfiguration()); // 2026-10-02-5ab2a
        modelBuilder.ApplyConfiguration(new PendingClarificationConfiguration()); // 2026-10-02-5ab2d
        modelBuilder.ApplyConfiguration(new WebhookLastSeenConfiguration()); // 2026-10-02-5ab2e
        modelBuilder.ApplyConfiguration(new ReworkNudgeConfiguration()); // 2026-10-08-0781
        modelBuilder.ApplyConfiguration(new ReworkLedgerConfiguration()); // 2026-10-08-0781
        modelBuilder.ApplyConfiguration(new SweepCursorConfiguration()); // 2026-10-08-9e6e
        modelBuilder.ApplyConfiguration(new PrSweepRepositoryConfiguration()); // 2026-10-08-10b0
        modelBuilder.ApplyConfiguration(new PrSweepStateConfiguration());
    }
}
