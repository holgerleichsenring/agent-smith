using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// p0349: the scoped EF worker behind <see cref="EfConfigDocumentStore"/>. Every
/// write is transactional (entity row + edges + audit version in one commit),
/// optimistic-concurrency checked, and secret-guarded; a delete is blocked while
/// the config_ref graph still points at the entity.
/// </summary>
public sealed class ConfigDocumentRepository(AgentSmithDbContext db)
{
    public bool IsEmpty() => !db.ConfigEntities.Any();

    public IReadOnlyList<ConfigDocRow> LoadAll() =>
        db.ConfigEntities.AsNoTracking()
            .Select(e => new ConfigDocRow(e.EntityType, e.EntityId, e.Doc, e.Version))
            .ToList();

    public void Save(ConfigDocWrite write)
    {
        SecretValueGuard.Validate(write.Type, write.Id, write.Doc);
        using var tx = db.Database.BeginTransaction();
        var existing = Find(write.Type, write.Id);
        var version = NextVersion(existing, write);
        UpsertRow(existing, write, version);
        ConfigEdgeWriter.Replace(db, write.Type, write.Id, write.Edges);
        AppendVersion(write.Type, write.Id, version, write.Doc, write.ChangedBy, write.Note);
        db.SaveChanges();
        tx.Commit();
    }

    public void Delete(string type, string id, string changedBy)
    {
        using var tx = db.Database.BeginTransaction();
        var referencing = ConfigEdgeWriter.Referencing(db, type, id);
        if (referencing.Count > 0)
            throw new ConfigurationException(
                $"Cannot delete {type} '{id}': referenced by {referencing.Count} " +
                $"entit{(referencing.Count == 1 ? "y" : "ies")} ({string.Join(", ", referencing)}).");
        var existing = Find(type, id);
        if (existing is null) { tx.Commit(); return; }
        ConfigEdgeWriter.RemoveOutgoing(db, type, id);
        db.ConfigEntities.Remove(existing);
        AppendVersion(type, id, existing.Version + 1, doc: null, changedBy, "deleted");
        db.SaveChanges();
        tx.Commit();
    }

    public IReadOnlyList<ConfigDocVersion> GetVersions() =>
        db.ConfigEntityVersions.AsNoTracking().OrderByDescending(v => v.Id).Select(ToVersion).ToList();

    public ConfigDocVersion? GetVersion(long versionId) =>
        db.ConfigEntityVersions.AsNoTracking().Where(v => v.Id == versionId).Select(ToVersion).SingleOrDefault();

    public string? PriorDoc(string type, string id, int beforeVersion) =>
        db.ConfigEntityVersions.AsNoTracking()
            .Where(v => v.EntityType == type && v.EntityId == id && v.Version < beforeVersion)
            .OrderByDescending(v => v.Version)
            .Select(v => v.Doc)
            .FirstOrDefault();

    private ConfigEntity? Find(string type, string id) =>
        db.ConfigEntities.SingleOrDefault(e => e.EntityType == type && e.EntityId == id);

    private static int NextVersion(ConfigEntity? existing, ConfigDocWrite write)
    {
        if (existing is null) return 1;
        if (write.ExpectedVersion is { } expected && expected != existing.Version)
            throw new StaleConfigVersionException(write.Type, write.Id, expected, existing.Version);
        return existing.Version + 1;
    }

    private void UpsertRow(ConfigEntity? existing, ConfigDocWrite write, int version)
    {
        if (existing is null)
        {
            db.ConfigEntities.Add(new ConfigEntity
            {
                EntityType = write.Type,
                EntityId = write.Id,
                Doc = write.Doc,
                Version = version,
                UpdatedBy = write.ChangedBy,
            });
            return;
        }
        existing.Doc = write.Doc;
        existing.Version = version;
        existing.UpdatedBy = write.ChangedBy;
    }

    private void AppendVersion(string type, string id, int version, string? doc, string changedBy, string? note) =>
        db.ConfigEntityVersions.Add(new ConfigEntityVersion
        {
            EntityType = type,
            EntityId = id,
            Version = version,
            Doc = doc,
            ChangedBy = changedBy,
            Note = note,
        });

    private static ConfigDocVersion ToVersion(ConfigEntityVersion v) =>
        new(v.Id, v.EntityType, v.EntityId, v.Version, v.Doc, v.ChangedBy, v.Note, v.CreatedAt);
}
