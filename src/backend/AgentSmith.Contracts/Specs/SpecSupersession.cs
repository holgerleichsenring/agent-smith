namespace AgentSmith.Contracts.Specs;

/// <summary>
/// 2026-11-27-3532d: a spec retired undelivered stays a member of its set and keeps its record; the member whose
/// <c>supersedes</c> names it takes its place. The replacement's line is the one record of the retirement — nothing
/// else stores it — so which specs are retired, and where a replacement stands, is read from the members alone.
/// </summary>
public static class SpecSupersession
{
    /// <summary>The ids of the members another member supersedes.</summary>
    public static IReadOnlySet<string> Retired(IReadOnlyList<DerivedSpec> specs)
    {
        ArgumentNullException.ThrowIfNull(specs);
        var ids = specs.Select(s => s.SpecId).ToHashSet(StringComparer.Ordinal);
        return specs.SelectMany(s => s.Draft.Supersedes).Where(ids.Contains).ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>The ids of the set's retired specs.</summary>
    public static IReadOnlySet<string> Retired(this SpecSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        return Retired(set.Specs);
    }

    /// <summary>The specs whose files lie in planned/: every spec that has not run, a retired one included, so a
    /// retired spec's text stays on the branch beside the spec that replaced it.</summary>
    public static IReadOnlyList<DerivedSpec> Planned(this SpecSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        return [.. set.Specs.Where(p => !set.Executed.Contains(p.SpecId, StringComparer.Ordinal))];
    }

    /// <summary>
    /// <paramref name="specs"/> in their order with every replacement moved right behind the spec it supersedes —
    /// where the retired spec would have run, so what required the retired spec and now requires its replacement
    /// still comes after it. A spec superseding no member keeps its place.
    /// </summary>
    public static IReadOnlyList<DerivedSpec> Ordered(IReadOnlyList<DerivedSpec> specs)
    {
        ArgumentNullException.ThrowIfNull(specs);
        var ids = specs.Select(s => s.SpecId).ToHashSet(StringComparer.Ordinal);
        var placed = new HashSet<string>(StringComparer.Ordinal);
        var ordered = new List<DerivedSpec>(specs.Count);
        foreach (var spec in specs.Where(s => !s.Draft.Supersedes.Any(ids.Contains)))
            Place(spec, specs, ordered, placed);
        // A replacement whose chain never reaches a spec superseding nothing (a cycle) keeps its own place.
        foreach (var spec in specs.Where(s => !placed.Contains(s.SpecId)))
            Place(spec, specs, ordered, placed);
        return ordered;
    }

    private static void Place(DerivedSpec spec, IReadOnlyList<DerivedSpec> specs, List<DerivedSpec> ordered, HashSet<string> placed)
    {
        if (!placed.Add(spec.SpecId)) return;
        ordered.Add(spec);
        foreach (var next in specs.Where(s => s.Draft.Supersedes.Contains(spec.SpecId, StringComparer.Ordinal)))
            Place(next, specs, ordered, placed);
    }
}
