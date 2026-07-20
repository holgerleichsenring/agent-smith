using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// p0356: reads the latest run row for a ticket that carries a persisted
/// progress ledger (mid-run flushes write it long before run end) — the
/// same-ticket RESUME seed. Singleton facade over a scope-per-operation read,
/// the DbActiveRunLease idiom. Corrupt/empty JSON degrades to null — resume is
/// an affordance, never a blocker.
/// </summary>
public sealed class DbPriorRunLedgerReader(IServiceScopeFactory scopeFactory) : IPriorRunLedgerReader
{
    public async Task<PriorRunLedger?> ReadLatestForTicketAsync(string ticketId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ticketId)) return null;
        using var scope = scopeFactory.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        // Ordered by Id: run ids are time-sortable by construction (p0156), and
        // SQLite cannot ORDER BY a DateTimeOffset column.
        var run = await uow.Set<Run>().AsNoTracking()
            .Where(r => r.TicketId == ticketId && r.ProgressLedgerJson != null)
            .OrderByDescending(r => r.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (run is null) return null;
        var items = RunStoryJson.TryDeserialize<List<ProgressLedgerItemView>>(run.ProgressLedgerJson);
        if (items is null || items.Count == 0) return null;
        return new PriorRunLedger(run.Id, run.StartedAt, items);
    }
}
