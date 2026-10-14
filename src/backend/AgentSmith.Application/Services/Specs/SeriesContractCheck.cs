using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-13-8762j: whether a series contract collides — every problem at once, because the epic gets one
/// re-prompt and the derivation shares its attempts with other checks; naming one collision per attempt would
/// spend them. Overlap itself is never refused: one owner per thing, one number per (kind, number), one budget
/// per (path, member), and a shared file only with every claimant's budget (<see cref="SeriesContractSharedFiles"/>).
/// </summary>
public sealed class SeriesContractCheck
{
    /// <param name="members">The set's members by spec id, each with the repositories it names.</param>
    /// <returns>Every problem joined into one message, or null when the contract is clean.</returns>
    public string? Problems(SeriesContract contract, IReadOnlyList<(string Id, IReadOnlyList<string> Repos)> members)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(members);
        var ids = members.Select(m => m.Id).ToHashSet(StringComparer.Ordinal);
        var problems = new List<string>();
        problems.AddRange(NonMembers(contract, ids));
        problems.AddRange(contract.Names.Where(n => SeriesContractKinds.Known(n.Kind) is null).Select(n =>
            $"name '{n.Name}' has kind '{n.Kind}', which is not one of {string.Join(", ", SeriesContractKinds.All)}"));
        problems.AddRange(OwnerClaims(contract));
        problems.AddRange(Numbers(contract));
        problems.AddRange(Budgets(contract));
        problems.AddRange(SeriesContractSharedFiles.Problems(contract, members));
        return problems.Count == 0 ? null : "the series contract collides: " + string.Join("; ", problems);
    }

    private static IEnumerable<string> NonMembers(SeriesContract contract, IReadOnlySet<string> ids)
    {
        var named = contract.Names.SelectMany(n => Roles($"name '{n.Name}'", n.Owner, n.Writers))
            .Concat(contract.Data.SelectMany(d => Roles($"datum '{d.Datum}'", d.Owner, d.Writers)))
            .Concat(contract.Areas.Select(a => (Entry: "an area", Role: "member", Id: a.Spec)))
            .Concat(contract.Budgets.Select(b => (Entry: $"the budget of {b.Path}", Role: "member", Id: b.Spec)));
        return named.Where(r => !ids.Contains(r.Id))
            .Select(r => $"{r.Entry} names {r.Role} '{r.Id}', which is not a spec of this set");
    }

    private static IEnumerable<(string Entry, string Role, string Id)> Roles(
        string entry, string owner, IReadOnlyList<string>? writers) =>
        new[] { (entry, "owner", owner) }.Concat((writers ?? []).Select(w => (entry, "writer", w)));

    private static IEnumerable<string> OwnerClaims(SeriesContract contract) =>
        contract.Data.GroupBy(d => d.Datum.Trim(), StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1)
            .Select(g => $"datum '{g.Key}' is claimed by {g.Count()} owner entries ({string.Join(", ", g.Select(d => d.Owner))}) "
                + "— name one owner and the others as writers")
            .Concat(contract.Names
                .GroupBy(n => (Kind: KindOf(n), Name: n.Name.Trim().ToLowerInvariant())).Where(g => g.Count() > 1)
                .Select(g => $"{g.Key.Kind} '{g.First().Name.Trim()}' is claimed by {g.Count()} owner entries "
                    + $"({string.Join(", ", g.Select(n => n.Owner))}) — name one owner and the others as writers"));

    private static IEnumerable<string> Numbers(SeriesContract contract) =>
        contract.Names.Where(n => n.Number is not null)
            .GroupBy(n => (Kind: KindOf(n), n.Number))
            .Where(g => g.Select(n => n.Name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
            .Select(g => $"{g.Key.Kind} {g.Key.Number} is given to {string.Join(" and ", g.Select(n => $"'{n.Name}'"))}");

    private static IEnumerable<string> Budgets(SeriesContract contract) =>
        contract.Budgets.Where(b => b.MaxLines < 1)
            .Select(b => $"the budget of {b.Path} for {b.Spec} is {b.MaxLines}, below 1")
            .Concat(contract.Budgets
                .GroupBy(b => (Path: SeriesContractSharedFiles.Normalize(b.Path), b.Spec))
                .Where(g => g.Select(b => b.MaxLines).Distinct().Count() > 1)
                .Select(g => $"{g.Key.Path} has two budgets for {g.Key.Spec} ({string.Join(", ", g.Select(b => b.MaxLines))})"));

    private static string KindOf(SeriesContractName name) =>
        SeriesContractKinds.Known(name.Kind) ?? name.Kind.Trim().ToLowerInvariant();
}
