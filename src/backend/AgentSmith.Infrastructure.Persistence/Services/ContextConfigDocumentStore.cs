using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-11-22-747da: the config-document store over ONE context, for `database migrate`, which builds its context by
/// hand rather than from a container. The server keeps <see cref="EfConfigDocumentStore"/>, a scope per operation.
/// </summary>
public sealed class ContextConfigDocumentStore(AgentSmithDbContext db) : IConfigDocumentStore
{
    private readonly ConfigDocumentRepository _documents = new(db);

    public bool IsEmpty() => _documents.IsEmpty();

    public IReadOnlyList<ConfigDocRow> LoadAll() => _documents.LoadAll();

    public void Save(ConfigDocWrite write) => _documents.Save(write);

    public void Delete(string type, string id, string changedBy) => _documents.Delete(type, id, changedBy);

    public void Import(IReadOnlyList<ConfigDocWrite> entities, bool force) =>
        new ConfigImportRepository(db).Import(entities, force);

    public IReadOnlyList<ConfigDocVersion> GetVersions() => _documents.GetVersions();

    public ConfigDocVersion? GetVersion(long versionId) => _documents.GetVersion(versionId);

    public string? PriorDoc(string type, string id, int beforeVersion) => _documents.PriorDoc(type, id, beforeVersion);
}
