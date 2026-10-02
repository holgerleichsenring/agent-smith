using System.Linq.Expressions;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// Ids only, and a scope per call: the caller is a singleton cold-start path, and it needs
/// the identities rather than the runs — hydrating each run's children to answer "which are
/// unfinished" would read the whole active estate to use none of it.
/// </summary>
public sealed class UnfinishedRunSource(IServiceScopeFactory scopeFactory) : IUnfinishedRunSource
{
    public Task<IReadOnlyList<string>> GetUnfinishedRunIdsAsync(CancellationToken cancellationToken) =>
        IdsAsync(r => r.FinishedAt == null, cancellationToken);

    public Task<IReadOnlyList<string>> GetUnparkedRunIdsAsync(CancellationToken cancellationToken) =>
        IdsAsync(r => r.FinishedAt == null && r.Status != RunStatuses.WaitingForInput, cancellationToken);

    private async Task<IReadOnlyList<string>> IdsAsync(
        Expression<Func<Run, bool>> filter, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IUnitOfWork>()
            .Set<Run>().AsNoTracking()
            .Where(filter)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);
    }
}
