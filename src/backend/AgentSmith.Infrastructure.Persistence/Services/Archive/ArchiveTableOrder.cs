using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Services.Archive;

/// <summary>
/// 2026-08-28-2af6: the order the tables are written in, derived from the declared model
/// so a table added next year orders itself. A table follows every table it references,
/// which is what makes an import's foreign keys hold at every point of the copy.
/// <para>
/// The model's graph is one edge — a config reference to its config entity — with no
/// cycles and no self-references, so this is a layered sweep and not a cycle-breaking
/// sort. If a future model ever did close a cycle, the remainder is appended rather than
/// looping forever; the import would then fail on the constraint, loudly.
/// </para>
/// </summary>
public sealed class ArchiveTableOrder
{
    public IReadOnlyList<IEntityType> Of(IModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var remaining = model.GetEntityTypes()
            .Where(t => t.GetTableName() is not null)
            .OrderBy(t => t.GetTableName(), StringComparer.Ordinal)
            .ToList();

        var ordered = new List<IEntityType>(remaining.Count);
        var placed = new HashSet<IEntityType>();
        while (remaining.Count > 0)
        {
            var ready = Ready(remaining, placed);
            ordered.AddRange(ready);
            foreach (var type in ready) placed.Add(type);
            remaining.RemoveAll(placed.Contains);
        }

        return ordered;
    }

    private static List<IEntityType> Ready(List<IEntityType> remaining, HashSet<IEntityType> placed)
    {
        var ready = remaining.Where(t => Principals(t).All(p => p == t || placed.Contains(p))).ToList();
        return ready.Count > 0 ? ready : remaining;
    }

    private static IEnumerable<IEntityType> Principals(IEntityType type) =>
        type.GetForeignKeys().Select(fk => fk.PrincipalEntityType);
}
