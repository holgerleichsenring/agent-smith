using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// p0349: writes the config reference graph — the edges owned by one entity. An
/// entity's outgoing edges are replaced wholesale on save; the referencing set
/// (incoming edges) gates a delete.
/// </summary>
internal static class ConfigEdgeWriter
{
    public static void Replace(AgentSmithDbContext db, string type, string id, IReadOnlyList<ConfigDocEdge> edges)
    {
        RemoveOutgoing(db, type, id);
        foreach (var edge in edges)
            db.ConfigRefs.Add(new ConfigRef
            {
                FromType = type,
                FromId = id,
                ToType = edge.ToType,
                ToId = edge.ToId,
            });
    }

    public static void RemoveOutgoing(AgentSmithDbContext db, string type, string id)
    {
        var outgoing = db.ConfigRefs.Where(r => r.FromType == type && r.FromId == id).ToList();
        db.ConfigRefs.RemoveRange(outgoing);
    }

    public static IReadOnlyList<string> Referencing(AgentSmithDbContext db, string type, string id) =>
        db.ConfigRefs
            .Where(r => r.ToType == type && r.ToId == id)
            .Select(r => r.FromType + "/" + r.FromId)
            .Distinct()
            .ToList();
}
