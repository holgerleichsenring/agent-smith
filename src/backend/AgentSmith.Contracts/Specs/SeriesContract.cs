namespace AgentSmith.Contracts.Specs;

/// <summary>
/// 2026-10-13-8762j: what the members of one series agree on before any of them is built — the names they
/// declare (and the numbers those take), the data they persist, the files each touches and each member's
/// line budget for a file it shares. Every member is named by its spec id.
/// </summary>
public sealed record SeriesContract(
    IReadOnlyList<SeriesContractName> Names,
    IReadOnlyList<SeriesContractDatum> Data,
    IReadOnlyList<SeriesContractArea> Areas,
    IReadOnlyList<SeriesContractBudget> Budgets)
{
    public bool IsEmpty => Names.Count == 0 && Data.Count == 0 && Areas.Count == 0 && Budgets.Count == 0;

    /// <summary>The contract with every spec id <paramref name="map"/> names replaced; an unmapped id stays.</summary>
    public SeriesContract Rekeyed(IReadOnlyDictionary<string, string> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        string Id(string id) => map.TryGetValue(id, out var mapped) ? mapped : id;
        IReadOnlyList<string>? Ids(IReadOnlyList<string>? ids) => ids is null ? null : [.. ids.Select(Id)];
        return new(
            [.. Names.Select(n => n with { Owner = Id(n.Owner), Writers = Ids(n.Writers) })],
            [.. Data.Select(d => d with { Owner = Id(d.Owner), Writers = Ids(d.Writers) })],
            [.. Areas.Select(a => a with { Spec = Id(a.Spec) })],
            [.. Budgets.Select(b => b with { Spec = Id(b.Spec) })]);
    }

    /// <summary>
    /// The contract with every entry whose owner or member is not one of <paramref name="members"/> removed,
    /// and every writer that is not one dropped from its entry.
    /// </summary>
    public SeriesContractWithin Within(IEnumerable<string> members)
    {
        ArgumentNullException.ThrowIfNull(members);
        var set = members.ToHashSet(StringComparer.Ordinal);
        var removed = new List<string>();
        bool Keeps(string id, string entry) { if (set.Contains(id)) return true; removed.Add($"{entry} (names {id})"); return false; }
        IReadOnlyList<string>? Writers(IReadOnlyList<string>? writers, string entry) =>
            writers?.Where(w => Keeps(w, $"writer of {entry}")).ToList();
        var narrowed = new SeriesContract(
            [.. Names.Where(n => Keeps(n.Owner, $"name {n.Kind} '{n.Name}'"))
                .Select(n => n with { Writers = Writers(n.Writers, $"name {n.Kind} '{n.Name}'") })],
            [.. Data.Where(d => Keeps(d.Owner, $"datum '{d.Datum}'"))
                .Select(d => d with { Writers = Writers(d.Writers, $"datum '{d.Datum}'") })],
            [.. Areas.Where(a => Keeps(a.Spec, "area"))],
            [.. Budgets.Where(b => Keeps(b.Spec, $"budget of {b.Path}"))]);
        return new(narrowed.IsEmpty ? null : narrowed, removed);
    }
}
