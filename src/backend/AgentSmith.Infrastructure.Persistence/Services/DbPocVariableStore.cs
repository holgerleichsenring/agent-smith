using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>2026-11-23-933dg: IPocVariableStore, a scope per operation over the scoped repository — the DbPocBuildStore
/// pattern.</summary>
public sealed class DbPocVariableStore(IServiceScopeFactory scopeFactory) : IPocVariableStore
{
    public async Task<PocVariableSubject?> ReadAsync(long pocId, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<PocVariableRepository>().ReadAsync(pocId, ct);
    }

    public async Task WritePlainAsync(long pocId, string? plainVariablesJson, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        await scope.ServiceProvider.GetRequiredService<PocVariableRepository>().WritePlainAsync(pocId, plainVariablesJson, ct);
    }
}
