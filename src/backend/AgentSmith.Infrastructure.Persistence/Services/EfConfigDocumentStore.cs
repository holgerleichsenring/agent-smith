using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// p0349: the singleton <see cref="IConfigDocumentStore"/> facade. The config store
/// is resolved by singletons (the server's DbConfigStore + config loader), so each
/// operation opens a scope and delegates to the scoped repositories — the same
/// scope-per-op idiom the run-state DB facades use (no IDbContextFactory).
/// </summary>
public sealed class EfConfigDocumentStore(IServiceScopeFactory scopeFactory) : IConfigDocumentStore
{
    public bool IsEmpty() => InScope(r => r.IsEmpty());

    public IReadOnlyList<ConfigDocRow> LoadAll() => InScope(r => r.LoadAll());

    public void Save(ConfigDocWrite write) => InScope(r => r.Save(write));

    public void Delete(string type, string id, string changedBy) => InScope(r => r.Delete(type, id, changedBy));

    public IReadOnlyList<ConfigDocVersion> GetVersions() => InScope(r => r.GetVersions());

    public ConfigDocVersion? GetVersion(long versionId) => InScope(r => r.GetVersion(versionId));

    public string? PriorDoc(string type, string id, int beforeVersion) =>
        InScope(r => r.PriorDoc(type, id, beforeVersion));

    public void Import(IReadOnlyList<ConfigDocWrite> entities, bool force)
    {
        using var scope = scopeFactory.CreateScope();
        scope.ServiceProvider.GetRequiredService<ConfigImportRepository>().Import(entities, force);
    }

    private T InScope<T>(Func<ConfigDocumentRepository, T> op)
    {
        using var scope = scopeFactory.CreateScope();
        return op(scope.ServiceProvider.GetRequiredService<ConfigDocumentRepository>());
    }

    private void InScope(Action<ConfigDocumentRepository> op)
    {
        using var scope = scopeFactory.CreateScope();
        op(scope.ServiceProvider.GetRequiredService<ConfigDocumentRepository>());
    }
}
