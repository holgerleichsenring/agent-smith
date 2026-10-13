using System.Globalization;
using AgentSmith.Contracts.Models.Structure;

namespace AgentSmith.Application.Services.Structure;

/// <summary>
/// 2026-10-06-8565b: reads the Limits a composed principles file declares. The delta's
/// '## Limits' fence comes first; a '### Limits' fence under Project Specifics is the house
/// standard and overrides it key by key, because it is read later. Only the closed metric keys
/// count, and only as positive integers — the catalog's validator refuses anything else, so a
/// stray value here sets no limit rather than an invented one.
/// 2026-10-12-fbbda: 'tolerance_percent' is read beside the metrics, as an integer of 0 or more —
/// a house fence overrides the delta's, 0 makes the context strict, and a fence that carries only
/// a tolerance sets no limit.
/// 2026-10-13-2d0c: values parse culture-invariant, and Project Specifics ends at the next '## '.
/// </summary>
public sealed class StructureLimits
{
    private const string DeltaHeading = "## Limits";
    private const string HouseHeading = "### Limits";
    private const string SpecificsHeading = "## Project Specifics";
    public const string ToleranceKey = "tolerance_percent";
    private const string SectionPrefix = "## ";

    public StructureLimitSet Read(string? principles)
    {
        if (string.IsNullOrEmpty(principles)) return StructureLimitSet.None;
        var merged = new Dictionary<string, int>(StringComparer.Ordinal);
        var tolerance = 0;
        foreach (var fence in Fences(principles.Split('\n')))
            tolerance = Merge(merged, fence) ?? tolerance;
        return new StructureLimitSet(merged, tolerance);
    }

    private static IEnumerable<IReadOnlyList<string>> Fences(string[] lines)
    {
        var inSpecifics = false;
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd();
            if (line.StartsWith(SectionPrefix, StringComparison.Ordinal))
                inSpecifics = line.StartsWith(SpecificsHeading, StringComparison.Ordinal);
            var isLimits = line == DeltaHeading || (inSpecifics && line == HouseHeading);
            if (isLimits && FenceAfter(lines, i + 1) is { } fence) yield return fence;
        }
    }

    // The first yaml fence before the next heading; a heading with none declares nothing.
    private static IReadOnlyList<string>? FenceAfter(string[] lines, int start)
    {
        var i = start;
        while (i < lines.Length && !lines[i].StartsWith("```yaml", StringComparison.Ordinal))
        {
            if (lines[i].StartsWith('#')) return null;
            i++;
        }
        var body = new List<string>();
        for (i++; i < lines.Length && !lines[i].StartsWith("```", StringComparison.Ordinal); i++)
            body.Add(lines[i]);
        return i < lines.Length ? body : null;
    }

    // Merges the fence's metrics and returns the tolerance it declares, null when none.
    private static int? Merge(Dictionary<string, int> merged, IReadOnlyList<string> fence)
    {
        int? tolerance = null;
        foreach (var raw in fence)
        {
            var parts = raw.Split('#', 2)[0].Split(':', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)) continue;
            if (parts[0] == ToleranceKey && value >= 0) tolerance = value;
            else if (StructureMetric.All.Contains(parts[0]) && value > 0) merged[parts[0]] = value;
        }
        return tolerance;
    }
}
