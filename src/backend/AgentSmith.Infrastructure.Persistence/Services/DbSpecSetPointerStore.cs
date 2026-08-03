using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// p0393a: ISpecSetPointerStore facade for singleton callers. Like
/// <see cref="DbRunCheckpointStore"/>, it opens a scope per operation and
/// delegates to the scoped repository.
/// </summary>
public sealed class DbSpecSetPointerStore(IServiceScopeFactory scopeFactory) : ISpecSetPointerStore
{
    public async Task<SpecSetPointer?> GetAsync(string project, string key, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<TicketSpecSetRepository>()
            .GetAsync(project, key, ct);
    }

    public async Task SaveAsync(string project, SpecSetPointer pointer, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        await scope.ServiceProvider.GetRequiredService<TicketSpecSetRepository>()
            .SaveAsync(project, pointer, ct);
    }
}
