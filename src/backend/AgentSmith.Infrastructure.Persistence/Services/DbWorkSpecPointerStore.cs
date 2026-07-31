using AgentSmith.Contracts.WorkSpecs;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// p0390: IWorkSpecPointerStore facade for singleton callers. Like
/// <see cref="DbRunCheckpointStore"/>, it opens a scope per operation and
/// delegates to the scoped repository.
/// </summary>
public sealed class DbWorkSpecPointerStore(IServiceScopeFactory scopeFactory) : IWorkSpecPointerStore
{
    public async Task<WorkSpecPointer?> GetAsync(string project, string key, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<TicketWorkSpecRepository>()
            .GetAsync(project, key, ct);
    }

    public async Task SaveAsync(string project, WorkSpecPointer pointer, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        await scope.ServiceProvider.GetRequiredService<TicketWorkSpecRepository>()
            .SaveAsync(project, pointer, ct);
    }
}
