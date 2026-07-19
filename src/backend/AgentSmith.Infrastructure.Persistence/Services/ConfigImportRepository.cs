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

        using var tx = db.Database.BeginTransaction();
        if (force) ClearCurrent();
        InsertEntities(entities);
        InsertEdges(entities);
        tx.Commit();
    }

    private void ClearCurrent()
    {
        db.ConfigRefs.RemoveRange(db.ConfigRefs);
        db.ConfigEntities.RemoveRange(db.ConfigEntities);
        db.SaveChanges();
    }

    private void InsertEntities(IReadOnlyList<ConfigDocWrite> entities)
    {
        foreach (var entity in entities)
        {
            var version = NextVersion(entity.Type, entity.Id);
            db.ConfigEntities.Add(new ConfigEntity
            {
                EntityType = entity.Type,
                EntityId = entity.Id,
                Doc = entity.Doc,
                Version = version,
                UpdatedBy = entity.ChangedBy,
            });
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
