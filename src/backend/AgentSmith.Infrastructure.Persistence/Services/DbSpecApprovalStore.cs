using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-09-17-0e79a: ISpecApprovalStore facade for singleton callers. Like
/// <see cref="DbSpecSetPointerStore"/>, it opens a scope per operation and delegates to the
/// scoped repository. The server swaps this in; every other process keeps the in-memory
/// default and relies on the carry.
/// </summary>
public sealed class DbSpecApprovalStore(IServiceScopeFactory scopeFactory) : ISpecApprovalStore
{
    public async Task<SpecApprovalRecord?> GetAsync(
        string tracker, string key, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApprovedSpecSetRepository>()
            .GetAsync(tracker, key, cancellationToken);
    }

    public async Task SaveAsync(SpecApprovalRecord record, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ApprovedSpecSetRepository>()
            .SaveAsync(record, cancellationToken);
    }

    public async Task<OutstandingApprovals> ListOutstandingAsync(
        string tracker, int limit, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApprovedSpecSetRepository>()
            .ListOutstandingAsync(tracker, limit, cancellationToken);
    }

    public async Task MarkSatisfiedAsync(
        string tracker, string key, DateTimeOffset at, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ApprovedSpecSetRepository>()
            .MarkSatisfiedAsync(tracker, key, at, cancellationToken);
    }
}
