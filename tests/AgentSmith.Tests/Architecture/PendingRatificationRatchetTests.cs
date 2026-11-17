using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// 2026-11-16-3596: no unratified decision ships — an open choice is asked before the commit,
/// never merged as pending ratification. The decision and spec files that carry the marker today
/// are listed with their count as debt: no file may join, no count may grow, a cleared file leaves.
/// This spec's own files quote the marker and are skipped by name.
/// </summary>
public sealed class PendingRatificationRatchetTests
{
    private const string BaselineFile = "pending-ratification-baseline.tsv";
    private const string OwnSpec = "2026-11-16-3596";

    [Fact]
    public void Methodology_ANewFile_CarriesNoPendingRatification()
    {
        var baseline = Baseline();
        var offenders = Counts().Where(c => !baseline.ContainsKey(c.Key)).Select(c => $"{c.Key}: {c.Value}").ToList();

        offenders.Should().BeEmpty(
            "an open choice is asked before the commit; ask the operator instead of marking the decision "
            + "pending ratification, and do not add a baseline row.\n  " + string.Join("\n  ", offenders));
    }

    [Fact]
    public void Methodology_NoListedCount_Grew()
    {
        var counts = Counts();
        var grown = Baseline()
            .Where(row => counts.TryGetValue(row.Key, out var now) && now > row.Value)
            .Select(row => $"{row.Key}: {row.Value} → {counts[row.Key]}")
            .ToList();

        grown.Should().BeEmpty(
            "a listed file may only lose markers; ask the operator instead.\n  " + string.Join("\n  ", grown));
    }

    [Fact]
    public void Methodology_AClearedFile_MustLeaveTheBaseline()
    {
        var counts = Counts();
        var stale = Baseline().Keys.Where(k => !counts.ContainsKey(k)).ToList();

        stale.Should().BeEmpty(
            $"a file without markers (or gone) must leave {BaselineFile}.\n  " + string.Join("\n  ", stale));
    }

    private static IReadOnlyDictionary<string, int> Counts() =>
        new[] { "decisions", "specs" }
            .SelectMany(dir => Directory.EnumerateFiles(
                Path.Combine(ArchitectureSources.AgentSmithRoot, dir), "*.yaml", SearchOption.AllDirectories))
            .Where(p => !Path.GetFileName(p).StartsWith(OwnSpec, StringComparison.Ordinal))
            .Select(p => (Key: Path.GetFileName(p), Count: PendingRatificationScanner.Count(File.ReadAllText(p))))
            .Where(x => x.Count > 0)
            .ToDictionary(x => x.Key, x => x.Count, StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, int> Baseline() =>
        File.ReadAllLines(Path.Combine(ArchitectureSources.TestSourceRoot, "Architecture", BaselineFile))
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(line => line.Split('\t'))
            .ToDictionary(parts => parts[1], parts => int.Parse(parts[0]), StringComparer.Ordinal);
}
