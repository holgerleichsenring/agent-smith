using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.Events;

/// <summary>
/// p0388b: the run's latest logged decisions, newest first, from the durable
/// RunDecision projection — so the Building beat's notes no longer depend on
/// decision events still sitting in the client's live event buffer.
/// </summary>
public sealed class RunDecisionsReader(IServiceScopeFactory scopeFactory)
{
    public async Task<IReadOnlyList<RunDecisionView>> ReadLatestAsync(
        string runId, int limit, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var rows = await uow.Set<RunDecision>().AsNoTracking()
            .Where(d => d.RunId == runId)
            .OrderByDescending(d => d.Id)
            .Take(limit)
            .ToListAsync(ct);
        return rows
            .Select(d => new RunDecisionView(d.StepIndex, d.Name, d.Reason, d.CreatedAt))
            .ToList();
    }
}
