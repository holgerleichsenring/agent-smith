using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-10-02-5ab2b: the IRunStartClaim facade for the singleton queue consumer — a scope per
/// claim over the scoped <see cref="QueuedRunRepository"/>, the shape of <see cref="DbRunHeartbeat"/>.
/// </summary>
public sealed class DbRunStartClaim(IServiceScopeFactory scopeFactory, TimeProvider timeProvider) : IRunStartClaim
{
    public async Task<RunStartClaimOutcome> ClaimAsync(string runId, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<QueuedRunRepository>()
            .ClaimAsync(runId, timeProvider.GetUtcNow(), cancellationToken);
    }
}
