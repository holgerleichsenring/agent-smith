using AgentSmith.Contracts.Models.Structure;

namespace AgentSmith.Application.Services.Structure;

/// <summary>
/// 2026-10-06-8565b: reads the Limits a composed principles file declares. The delta's
/// '## Limits' fence comes first; a '### Limits' fence under Project Specifics is the house
/// standard and overrides it key by key, because it is read later. Only the closed metric keys
/// count, and only as positive integers — the catalog's validator refuses anything else, so a
/// stray value here sets no limit rather than an invented one.
/// </summary>
public sealed class StructureLimits
{
    private const string DeltaHeading = "## Limits";
    private const string HouseHeading = "### Limits";
    private const string SpecificsHeading = "## Project Specifics";

    public StructureLimitSet Read(string? principles)
    {
        if (string.IsNullOrEmpty(principles)) return StructureLimitSet.None;
        var merged = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var fence in Fences(principles.Split('\n')))
            Merge(merged, fence);
        return new StructureLimitSet(merged);
    }

    private static IEnumerable<IReadOnlyList<string>> Fences(string[] lines)
    {
        var inSpecifics = false;
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd();
            if (line.StartsWith(SpecificsHeading, StringComparison.Ordinal)) inSpecifics = true;
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

    private static void Merge(Dictionary<string, int> merged, IReadOnlyList<string> fence)
    {
        foreach (var raw in fence)
        {
            var line = raw.Split('#', 2)[0];
            var parts = line.Split(':', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || !StructureMetric.All.Contains(parts[0])) continue;
            if (int.TryParse(parts[1], out var value) && value > 0) merged[parts[0]] = value;
        }
    }
}
