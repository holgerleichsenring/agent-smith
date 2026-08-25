using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Contracts;

namespace AgentSmith.Tests.ConfigStudio;

/// <summary>
/// 2026-08-25-1806: a document store that cannot be reached at all — an unmigrated schema,
/// a database that is not up yet. The migration has to survive it with the bootstrap
/// mapping still in force, because dropping the mapping is how an upgrade locks people out.
/// </summary>
internal sealed class UnreachableDocuments : IConfigDocumentStore
{
    private static InvalidOperationException Down() => new("no such table: config_entity");

    public bool IsEmpty() => throw Down();
    public IReadOnlyList<ConfigDocRow> LoadAll() => throw Down();
    public void Save(ConfigDocWrite write) => throw Down();
    public void Delete(string type, string id, string changedBy) => throw Down();
    public void Import(IReadOnlyList<ConfigDocWrite> entities, bool force) => throw Down();
    public IReadOnlyList<ConfigDocVersion> GetVersions() => throw Down();
    public ConfigDocVersion? GetVersion(long versionId) => throw Down();
    public string? PriorDoc(string type, string id, int beforeVersion) => throw Down();
}

/// <summary>The same outage seen from the role resolver's side: no stored mapping to read.</summary>
internal sealed class UnreadableStoredMapping : IStoredRoleMapping
{
    public RoleMappingConfig? Read() => null;
}
