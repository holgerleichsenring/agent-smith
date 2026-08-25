using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>
/// 2026-08-25-61f1: the store refuses to hold one run event's record twice. Every row a
/// run event produces carries the event's trail position, and (RunId, EventSeq) is unique
/// on each table the applier inserts into — so a replayed event cannot multiply the sums
/// the dashboard and the cost rollups read out of them.
/// <para>
/// Rows written before this phase carry no position, and SQL Server is the one provider
/// that treats two NULLs as equal in a unique index. There the index is filtered to the
/// rows that HAVE a position; everywhere else NULLs are already distinct. The provider is
/// asked by name so neither model snapshot has to be told a lie about the other's.
/// </para>
/// </summary>
public sealed class RunRecordIdentityConfiguration(string? providerName)
{
    private const string SqlServerProvider = "Microsoft.EntityFrameworkCore.SqlServer";
    private const string SqlServerFilter = "[EventSeq] IS NOT NULL";

    public void Apply(ModelBuilder modelBuilder)
    {
        Unique<RunStep>(modelBuilder);
        Unique<RunLlmCall>(modelBuilder);
        Unique<RunDecision>(modelBuilder);
    }

    private void Unique<T>(ModelBuilder modelBuilder) where T : class
    {
        var index = modelBuilder.Entity<T>().HasIndex("RunId", "EventSeq").IsUnique();
        if (providerName == SqlServerProvider) index.HasFilter(SqlServerFilter);
    }
}
