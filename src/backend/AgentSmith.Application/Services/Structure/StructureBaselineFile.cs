using System.Globalization;
using AgentSmith.Contracts.Models.Structure;

namespace AgentSmith.Application.Services.Structure;

/// <summary>
/// 2026-10-06-8565c: structure-baseline.tsv beside a context's principles.md — one tab-separated
/// line per tolerated unit, sorted, so a diff of the file reads as what got better.
/// 2026-10-13-2d0c: the header also records the Limits the entries were measured with.
/// 2026-10-13-50c7: and the directories the context owned; a header line it has nothing for is left out.
/// 2026-11-24-8756: and '# counted: breaks' when its values include the breaks a formatter would add.
/// </summary>
public sealed class StructureBaselineFile(StructureBaselineHeader header, StructureBaselineOwnedLine ownedLine)
{
    public const string FileName = "structure-baseline.tsv";
    private const string Header = "# path\tkey\tmetric\tvalue — units over their Limits when measured, each tolerated up to its value";
    private const string CountedLine = "# counted: breaks — a unit's lines include the line breaks a formatter would add";
    private const string CountedPrefix = "# counted: breaks";

    public IReadOnlyList<StructureBaselineEntry> Parse(string? text) =>
        [.. (text ?? string.Empty).Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .Select(l => l.Split('\t'))
            .Where(f => f.Length == 4 && int.TryParse(f[3], CultureInfo.InvariantCulture, out _))
            .Select(f => new StructureBaselineEntry(f[0], f[1], f[2], int.Parse(f[3], CultureInfo.InvariantCulture)))];

    /// <summary>The Limits the baseline was measured with, or null when it records none.</summary>
    public StructureLimitSet? MeasuredWith(string? text) => header.Read(text);

    /// <summary>2026-11-24-8756: whether the values were counted with breaks; a baseline written before is not.</summary>
    public bool CountsBreaks(string? text) =>
        (text ?? string.Empty).Split('\n').Any(l => l.StartsWith(CountedPrefix, StringComparison.Ordinal));

    public string Render(IEnumerable<StructureBaselineEntry> entries, StructureLimitSet? measuredWith,
        IReadOnlyList<string>? owned = null, bool countsBreaks = true) =>
        string.Join('\n', new[] { Header, countsBreaks ? CountedLine : null,
                measuredWith is null ? null : header.Render(measuredWith),
                owned is null ? null : ownedLine.Render(owned) }.OfType<string>().Concat(entries
            .OrderBy(e => e.Path, StringComparer.Ordinal).ThenBy(e => e.Key, StringComparer.Ordinal)
            .ThenBy(e => e.Metric, StringComparer.Ordinal)
            .Select(e => $"{e.Path}\t{e.Key}\t{e.Metric}\t{e.Value.ToString(CultureInfo.InvariantCulture)}"))) + "\n";

    /// <summary>
    /// A refresh: every old entry still measured over its limit stays, at the lower of the two
    /// values. A unit not yet listed is added only when the old limits did not limit its metric or
    /// it was within that old limit's band — what a changed limit exposes. A unit grown past an
    /// unchanged band is not added; it surfaces at the commit gate. Old limits that are unknown
    /// (a baseline that records none) add every unit over its band: a limit is green on the day
    /// it is installed. <paramref name="measured"/> holds excesses only (2026-10-12-fbbda).
    /// 2026-11-24-8756: an old baseline's values are physical lines (<paramref name="oldCountsBreaks"/>
    /// false): an entry becomes its value plus the unit's breaks now, so growth since stays visible, and
    /// exposure is judged on the physical value.
    /// </summary>
    public IReadOnlyList<StructureBaselineEntry> Refresh(IReadOnlyList<StructureBaselineEntry> old,
        IReadOnlyList<StructureFinding> measured, StructureLimitSet? oldLimits, bool oldCountsBreaks = true)
    {
        var now = measured.GroupBy(f => (f.Path, f.Key, f.Metric)).ToDictionary(g => g.Key, g => g.MaxBy(f => f.Value)!);
        var listed = old.Select(e => e.Identity).ToHashSet();
        var kept = old.Where(e => now.ContainsKey(e.Identity))
            .Select(e => e with { Value = Math.Min(now[e.Identity].Value, Counted(e.Value, now[e.Identity], oldCountsBreaks)) });
        var exposed = measured
            .Where(f => !listed.Contains((f.Path, f.Key, f.Metric)) && Exposed(f, oldLimits, oldCountsBreaks))
            .Select(StructureBaselineEntry.Of);
        return [.. kept.Concat(exposed).DistinctBy(e => e.Identity)];
    }

    /// <summary>2026-10-13-2d0c: the entries keyed by path+key+metric, for the gate's lookups.</summary>
    public IReadOnlyDictionary<(string, string, string), int> Index(IReadOnlyList<StructureBaselineEntry> entries) =>
        entries.GroupBy(e => e.Identity).ToDictionary(g => g.Key, g => g.Max(e => e.Value));

    // An entry in physical lines, translated into a count by the breaks the unit has now.
    private static int Counted(int entry, StructureFinding now, bool oldCountsBreaks) =>
        oldCountsBreaks || now.PhysicalValue is not { } physical ? entry : entry + (now.Value - physical);

    private static bool Exposed(StructureFinding finding, StructureLimitSet? oldLimits, bool oldCountsBreaks) =>
        oldLimits?.For(finding.Metric) is not { } before
        || (oldCountsBreaks ? finding.Value : finding.PhysicalValue ?? finding.Value) <= oldLimits.BandEnd(before);
}
