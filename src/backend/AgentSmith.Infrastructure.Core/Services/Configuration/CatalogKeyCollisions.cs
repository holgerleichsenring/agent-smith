using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// p0515: names the keys of one catalog that differ only in case, and says which of them
/// must not be built. Once a catalog is keyed by <see cref="ConfigNames.Comparer"/>, two
/// such keys are one key — the second write would silently overwrite the first and a
/// reference to either would resolve to whichever survived. Neither half is built, so no
/// reference resolves to a coin flip, and the pair is reported instead of guessed at.
/// <para>
/// The finding carries the CATALOG and the colliding spellings, never a project slot:
/// <see cref="StartupFinding.Identity"/> is deduped on, and a repo named like a project
/// would silence that project's triggers. The catalog plus the first spelling makes each
/// collision its own identity, so two collisions are two lines.
/// </para>
/// </summary>
public sealed class CatalogKeyCollisions
{
    public IReadOnlySet<string> Detect(
        string catalog, IEnumerable<string> keys, List<StartupFinding> findings)
    {
        var dropped = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in keys.GroupBy(k => k, ConfigNames.Comparer))
        {
            var spellings = group.OrderBy(k => k, StringComparer.Ordinal).ToList();
            if (spellings.Count < 2) continue;
            foreach (var spelling in spellings) dropped.Add(spelling);
            findings.Add(Collision(catalog, spellings));
        }
        return dropped;
    }

    private static StartupFinding Collision(string catalog, IReadOnlyList<string> spellings) =>
        new(StartupSubsystems.Configuration, StartupFindingSeverity.Blocking,
            $"Catalog '{catalog}' defines {spellings.Count} keys that differ only in case: "
            + string.Join(", ", spellings.Select(s => $"'{s}'"))
            + ". A configured name is matched without regard to case, so nothing can tell "
            + "them apart — none of them is loaded, and every project referencing one drops "
            + "out with it. Rename them so each name is unique regardless of case.",
            Field: $"{catalog}:{spellings[0]}");
}
