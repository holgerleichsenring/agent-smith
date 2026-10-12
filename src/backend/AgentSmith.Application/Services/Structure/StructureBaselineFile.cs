using System.Globalization;
using AgentSmith.Contracts.Models.Structure;

namespace AgentSmith.Application.Services.Structure;

/// <summary>
/// 2026-10-06-8565c: structure-baseline.tsv beside a context's principles.md — one tab-separated
/// line per tolerated unit, sorted, so a diff of the file reads as what got better.
/// </summary>
public static class StructureBaselineFile
{
    public const string FileName = "structure-baseline.tsv";
    private const string Header = "# path\tkey\tmetric\tvalue — units over their Limits when measured, each tolerated up to its value";

    public static IReadOnlyList<StructureBaselineEntry> Parse(string? text) =>
        [.. (text ?? string.Empty).Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .Select(l => l.Split('\t'))
            .Where(f => f.Length == 4 && int.TryParse(f[3], CultureInfo.InvariantCulture, out _))
            .Select(f => new StructureBaselineEntry(f[0], f[1], f[2], int.Parse(f[3], CultureInfo.InvariantCulture)))];

    public static string Render(IEnumerable<StructureBaselineEntry> entries) =>
        string.Join('\n', new[] { Header }.Concat(entries
            .OrderBy(e => e.Path, StringComparer.Ordinal).ThenBy(e => e.Key, StringComparer.Ordinal)
            .ThenBy(e => e.Metric, StringComparer.Ordinal)
            .Select(e => $"{e.Path}\t{e.Key}\t{e.Metric}\t{e.Value.ToString(CultureInfo.InvariantCulture)}"))) + "\n";

    /// <summary>
    /// A refresh: every old entry still measured over its limit stays, at the lower of the two
    /// values. A unit not yet listed is added only when the old principles did not limit its
    /// metric or it was within that old limit — what a changed limit exposes. A unit grown past
    /// an unchanged limit is not added; it surfaces at the commit gate.
    /// </summary>
    public static IReadOnlyList<StructureBaselineEntry> Refresh(IReadOnlyList<StructureBaselineEntry> old,
        IReadOnlyList<StructureFinding> measured, StructureLimitSet oldLimits)
    {
        var now = measured.GroupBy(f => (f.Path, f.Key, f.Metric)).ToDictionary(g => g.Key, g => g.Max(f => f.Value));
        var listed = old.Select(e => e.Identity).ToHashSet();
        var kept = old.Where(e => now.ContainsKey(e.Identity))
            .Select(e => e with { Value = Math.Min(e.Value, now[e.Identity]) });
        var exposed = measured.Where(f => !listed.Contains((f.Path, f.Key, f.Metric))
                && (oldLimits.For(f.Metric) is not { } before || f.Value <= before))
            .Select(StructureBaselineEntry.Of);
        return [.. kept.Concat(exposed)];
    }

    /// <summary>Whether the baseline tolerates a finding: the same unit, not grown past its entry.</summary>
    public static bool Tolerates(IReadOnlyList<StructureBaselineEntry> baseline, string path, string key,
        string metric, int value) =>
        baseline.Any(e => e.Path == path && e.Key == key && e.Metric == metric && value <= e.Value);
}
