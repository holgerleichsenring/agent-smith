using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// p0330: the IRunCancelStateReader facade for singleton entry points (queue
/// consumer, capacity pump). Opens a SCOPE per read and delegates to the scoped
/// <see cref="RunRepository"/> — same shape as <see cref="DbActiveRunLease"/>.
/// </summary>
public sealed class DbRunCancelStateReader(IServiceScopeFactory scopeFactory) : IRunCancelStateReader
{
    public async Task<bool> IsCancelRequestedAsync(string runId, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<RunRepository>()
            .IsCancelRequestedAsync(runId, cancellationToken);
    }
}
