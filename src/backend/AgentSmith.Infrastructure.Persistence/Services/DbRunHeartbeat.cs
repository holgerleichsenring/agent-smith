using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-10-02-5f89e: the IRunHeartbeat facade for the singleton heartbeat pump. Opens a
/// SCOPE per renewal and delegates to the scoped <see cref="RunLivenessRepository"/> —
/// the shape of <see cref="DbActiveRunLease"/>.
/// </summary>
public sealed class DbRunHeartbeat(IServiceScopeFactory scopeFactory, TimeProvider timeProvider) : IRunHeartbeat
{
    public async Task RenewAsync(string runId, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        await scope.ServiceProvider.GetRequiredService<RunLivenessRepository>()
            .RenewHeartbeatAsync(runId, timeProvider.GetUtcNow(), cancellationToken);
    }
}
