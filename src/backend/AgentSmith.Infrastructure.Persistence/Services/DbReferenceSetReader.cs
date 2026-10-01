using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-10-01-283dc: <see cref="IReferenceSetReader"/> for the singleton sandbox path. It opens a
/// scope per read and delegates to the scoped repository, like the other Db stores.
/// </summary>
public sealed class DbReferenceSetReader(IServiceScopeFactory scopeFactory) : IReferenceSetReader
{
    public async Task<IReadOnlyList<ReferenceSetFile>> FilesAsync(
        string sessionId, string setId, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var files = await scope.ServiceProvider.GetRequiredService<ReferenceSetRepository>()
            .FilesAsync(sessionId, setId, cancellationToken);
        return [.. files.Select(f => new ReferenceSetFile(f.Path, f.Content))];
    }
}
