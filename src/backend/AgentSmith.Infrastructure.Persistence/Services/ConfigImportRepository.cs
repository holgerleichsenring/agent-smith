using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// p0349: the guarded config import — the DR + cutover path (NOT auto-seed). Imports
/// into an EMPTY store, or with force into a non-empty one (bumping versions past
/// the existing history). ALL entity rows are inserted before ANY edges, so the
/// config_ref RESTRICT is satisfied regardless of the file's declaration order.
/// 2026-11-10-fcd7a: with force, the edges are cleared first, an entity the file still holds is UPDATED by its
/// natural key and only the ones it lacks are deleted — so a surviving project keeps its Id, and the memories
/// cascading from it.
/// </summary>
public sealed class ConfigImportRepository(AgentSmithDbContext db)
{
    public void Import(IReadOnlyList<ConfigDocWrite> entities, bool force)
    {
        if (!force && db.ConfigEntities.Any())
            throw new ConfigurationException(
                "Config store is not empty; pass --force to overwrite it (versions are bumped, history kept).");
        foreach (var entity in entities)
            SecretValueGuard.Validate(entity.Type, entity.Id, entity.Doc);
        // p0515b: the force path clears the current rows, so the incoming file is the whole
        // truth — and it is checked before the transaction opens, so a colliding pair cannot
        // wipe a store it was never going to replace.
        ConfigNameCollisionGuard.WithinSet(entities);

        using var tx = db.Database.BeginTransaction();
        if (force) ClearMissing(entities);
        WriteEntities(entities);
        InsertEdges(entities);
        tx.Commit();
    }

    private void ClearMissing(IReadOnlyList<ConfigDocWrite> entities)
    {
        db.ConfigRefs.RemoveRange(db.ConfigRefs);
        db.SaveChanges();
        var kept = entities.Select(e => (e.Type, e.Id)).ToHashSet();
        db.ConfigEntities.RemoveRange(db.ConfigEntities.AsEnumerable().Where(e => !kept.Contains((e.EntityType, e.EntityId))));
        db.SaveChanges();
    }

    private void WriteEntities(IReadOnlyList<ConfigDocWrite> entities)
    {
        foreach (var entity in entities)
        {
            var version = NextVersion(entity.Type, entity.Id);
            var row = db.ConfigEntities.FirstOrDefault(e => e.EntityType == entity.Type && e.EntityId == entity.Id);
            if (row is null) db.ConfigEntities.Add(row = new ConfigEntity { EntityType = entity.Type, EntityId = entity.Id });
            row.Doc = entity.Doc;
            row.Version = version;
            row.UpdatedBy = entity.ChangedBy;
            db.ConfigEntityVersions.Add(new ConfigEntityVersion
            {
                EntityType = entity.Type,
                EntityId = entity.Id,
                Version = version,
                Doc = entity.Doc,
                ChangedBy = entity.ChangedBy,
                Note = "import",
            });
        }
        db.SaveChanges();
    }

    private void InsertEdges(IReadOnlyList<ConfigDocWrite> entities)
    {
        foreach (var entity in entities)
            ConfigEdgeWriter.Replace(db, entity.Type, entity.Id, entity.Edges);
        db.SaveChanges();
    }

    private int NextVersion(string type, string id)
    {
        var max = db.ConfigEntityVersions
            .Where(v => v.EntityType == type && v.EntityId == id)
            .Select(v => (int?)v.Version)
            .Max();
        return (max ?? 0) + 1;
    }
}
