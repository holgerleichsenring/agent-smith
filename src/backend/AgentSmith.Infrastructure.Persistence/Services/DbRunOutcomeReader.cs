using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// Reads a run's standing from its row and its repos' pull requests. A run has ended when its
/// row carries FinishedAt — a waiting status (queued, waiting for input) keeps it null. Opens a
/// scope per read, for singleton callers.
/// </summary>
public sealed class DbRunOutcomeReader(IServiceScopeFactory scopeFactory) : IRunOutcomeReader
{
    public async Task<RunOutcome?> ReadAsync(string runId, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var run = await uow.Set<Run>().AsNoTracking()
            .Where(r => r.Id == runId)
            .Select(r => new { r.Status, r.FinishedAt, r.Summary })
            .FirstOrDefaultAsync(cancellationToken);
        if (run is null) return null;

        var prUrls = await uow.Set<RunRepo>().AsNoTracking()
            .Where(r => r.RunId == runId && r.PrUrl != null && r.PrUrl != "")
            .OrderBy(r => r.RepoName)
            .Select(r => r.PrUrl!)
            .ToListAsync(cancellationToken);
        var ended = run.FinishedAt is not null && !RunStatuses.IsWaiting(run.Status);
        return new RunOutcome(run.Status, ended, run.Summary, prUrls);
    }
}
