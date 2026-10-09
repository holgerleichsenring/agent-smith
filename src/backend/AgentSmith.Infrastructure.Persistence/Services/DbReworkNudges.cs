using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-10-08-0781: the queue and the watermark for singleton callers, a scope per operation. A
/// withheld run is looked up here, so callers name only the run: a code run with a ticket withholds
/// the acts on that ticket up to <c>at</c>; any other run withholds nothing.
/// </summary>
public sealed class DbReworkNudges(IServiceScopeFactory scopes) : IReworkNudges, IReworkWatermark
{
    public async Task EnqueueAsync(ReworkNudgeRequest request, CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ReworkNudgeRepository>().EnqueueAsync(request, cancellationToken);
    }

    public async Task WithholdRunAsync(string runId, DateTimeOffset at, CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        var run = await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().Set<Run>().AsNoTracking().Where(r => r.Id == runId)
            .Select(r => new { r.Project, r.TicketId, r.Pipeline }).FirstOrDefaultAsync(cancellationToken);
        if (run is null || run.Pipeline != PipelinePresets.CodeName || string.IsNullOrEmpty(run.TicketId)) return;
        await scope.ServiceProvider.GetRequiredService<ReworkLedgerRepository>().RaiseAsync(run.Project, run.TicketId, at, cancellationToken);
    }
}
