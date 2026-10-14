using System.Text.RegularExpressions;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-13-8762n: where a build note goes against the series contract — a mechanical hint, shown and never
/// enforced. A persisted entry naming a datum another spec owns without this spec among its writers; a
/// renamed entry whose old side names a contract name; a move into another spec's area and none of this
/// spec's (areas are repository-relative, so another spec's area counts only when their repositories
/// intersect — none named intersects all). A name matches by whole word, case-insensitively. A deviation the
/// note's own deviations name is DECLARED.
/// </summary>
public static class BuildNoteContractCheck
{
    public static IReadOnlyList<BuildNoteDeviation> Deviations(SpecBuildNote note, string specId, SpecSet set)
    {
        ArgumentNullException.ThrowIfNull(note);
        ArgumentNullException.ThrowIfNull(set);
        if (set.Contract is not { } contract) return [];
        var found = new List<(string Entry, string Subject, string Reason)>();
        foreach (var entry in note.Persisted)
            foreach (var datum in contract.Data.Where(d => d.Owner != specId && d.Writers?.Contains(specId) != true
                         && Names(entry, d.Datum)))
                found.Add((entry, datum.Datum, $"persists '{datum.Datum}', which the contract gives to {datum.Owner}"));
        foreach (var entry in note.Renamed)
            foreach (var name in contract.Names.Where(n => Names(Side(entry, 0), n.Name)))
                found.Add((entry, name.Name, $"renames the contract's {name.Kind} '{name.Name}' (owner {name.Owner})"));
        foreach (var entry in note.Moved)
            if (Intruded(contract, set, specId, SeriesContractSharedFiles.Normalize(Side(entry, 1))) is { } owner)
                found.Add((entry, Side(entry, 1), $"moves into {Side(entry, 1)}, in the area of {owner}"));
        return [.. found.Select(f => new BuildNoteDeviation(f.Entry, f.Reason,
            note.Deviations.Any(d => Names(d, f.Subject))))];
    }

    // The spec whose area the destination lies in when it lies in none of this spec's; null otherwise.
    private static string? Intruded(SeriesContract contract, SpecSet set, string specId, string destination)
    {
        var areas = contract.Areas.Where(a => a.Paths.Any(p => Under(destination, p))).ToList();
        if (destination.Length == 0 || areas.Any(a => a.Spec == specId)) return null;
        var mine = Repos(set, specId);
        return areas.FirstOrDefault(a => Intersect(mine, Repos(set, a.Spec)))?.Spec;
    }

    private static bool Under(string destination, string area)
    {
        var path = SeriesContractSharedFiles.Normalize(area).TrimEnd('/');
        return path.Length > 0 && (destination == path || destination.StartsWith(path + "/", StringComparison.Ordinal));
    }

    private static IReadOnlyList<string> Repos(SpecSet set, string specId) =>
        set.Specs.FirstOrDefault(s => s.SpecId == specId)?.Draft.Repos ?? [];

    private static bool Intersect(IReadOnlyList<string> a, IReadOnlyList<string> b) =>
        a.Count == 0 || b.Count == 0 || a.Intersect(b, StringComparer.Ordinal).Any();

    private static string Side(string entry, int side) =>
        entry.Split(HandForwardToolHost.Arrow, 2) is [var old, var to] ? (side == 0 ? old : to).Trim() : side == 0 ? entry : string.Empty;

    private static bool Names(string text, string term) =>
        term.Trim().Length > 0
        && Regex.IsMatch(text, $@"(?<![\w]){Regex.Escape(term.Trim())}(?![\w])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}

/// <summary>2026-10-13-8762n: one build-note entry that goes against the series contract, why, and whether
/// the note's own deviations name it.</summary>
public sealed record BuildNoteDeviation(string Entry, string Reason, bool Declared);
