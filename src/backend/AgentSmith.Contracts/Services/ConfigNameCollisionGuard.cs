using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Exceptions;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0515b: the config store refuses to hold two spellings of one name. A configured name is
/// matched by <see cref="ConfigNames"/>, so a write whose stored match is a DIFFERENT spelling
/// replaces an entity the operator never named: the lookup finds the twin, the upsert takes its
/// UPDATE branch, and the version map holds nothing for the id that was sent — so the stale
/// check that would have caught it never runs. Refusing the write is the whole fix; nothing
/// about the schema is involved.
/// <para>
/// The refusal names BOTH spellings because they are the operator's only handle on the pair.
/// A colliding pair is dropped from the catalog whole, so neither half is listed in the studio
/// and there is no row to click: export, edit, force-import is the way out, and it works
/// because the import clears the rows before it writes.
/// </para>
/// </summary>
public static class ConfigNameCollisionGuard
{
    /// <summary>Refuses an incoming name whose already-stored twin is spelled differently.</summary>
    public static void AgainstStored(string type, string id, IEnumerable<string> storedIds)
    {
        foreach (var stored in storedIds)
            if (IsTwin(stored, id))
                throw Collision(type, stored, id);
    }

    /// <summary>Refuses an imported set that carries two spellings of one name in one catalog.</summary>
    public static void WithinSet(IEnumerable<ConfigDocWrite> entities)
    {
        var firstSpelling = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        foreach (var entity in entities)
        {
            var perType = firstSpelling.TryGetValue(entity.Type, out var existing)
                ? existing
                : firstSpelling[entity.Type] = new Dictionary<string, string>(ConfigNames.Comparer);
            if (perType.TryGetValue(entity.Id, out var first) && IsTwin(first, entity.Id))
                throw Collision(entity.Type, first, entity.Id);
            perType.TryAdd(entity.Id, entity.Id);
        }
    }

    private static bool IsTwin(string stored, string incoming) =>
        ConfigNames.AreSame(stored, incoming)
        && !string.Equals(stored, incoming, StringComparison.Ordinal);

    private static ConfigurationException Collision(string type, string stored, string incoming) =>
        new($"Config {type} '{incoming}' collides with '{stored}', which the {type} catalog already "
            + "holds: a configured name is matched without regard to case, so the two spell one "
            + "entity and neither half of a colliding pair is listed in the studio. Nothing was "
            + $"written. Export the config to YAML, keep exactly one of '{stored}' and '{incoming}', "
            + "then import it back with force.");
}
