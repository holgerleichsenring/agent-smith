using AgentSmith.Contracts.Models.ConfigStudio;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0349: the low-level entity-document store behind the server's DbConfigStore.
/// Persists each config entity as a JSON doc row plus its derived reference edges,
/// with a single append-only version history as the audit. Save is transactional
/// (row + edges + version in one unit of work), optimistic-concurrency checked, and
/// secret-guarded; Delete is blocked when the entity is still referenced. The CLI
/// never binds this — it stays purely file-based.
/// </summary>
public interface IConfigDocumentStore
{
    /// <summary>True when no config entity rows exist — the server boots unconfigured.</summary>
    bool IsEmpty();

    /// <summary>All current doc rows, for assembling the typed config.</summary>
    IReadOnlyList<ConfigDocRow> LoadAll();

    /// <summary>Upsert one entity (doc + edges + a new version), version-checked and secret-guarded.</summary>
    void Save(ConfigDocWrite write);

    /// <summary>Delete one entity; rejected with the referencing set when an edge still points at it.</summary>
    void Delete(string type, string id, string changedBy);

    /// <summary>Import a whole config: all entity rows first, then all edges, so RESTRICT holds regardless of order.</summary>
    void Import(IReadOnlyList<ConfigDocWrite> entities, bool force);

    /// <summary>The single audit feed, newest first.</summary>
    IReadOnlyList<ConfigDocVersion> GetVersions();

    /// <summary>One audit row by its id — the revert target.</summary>
    ConfigDocVersion? GetVersion(long versionId);

    /// <summary>The doc of the newest version strictly below <paramref name="beforeVersion"/>, or null if none (a tombstone or the first version).</summary>
    string? PriorDoc(string type, string id, int beforeVersion);
}
