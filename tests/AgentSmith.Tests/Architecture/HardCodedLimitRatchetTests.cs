using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// 2026-11-17-274cc: rule 4 — a new limit earns its place: a reason the operator would accept, a
/// config key, and a message naming the value, the reason and the key. The hard-coded bounds that
/// predate the rule are listed per file as debt (the inventory note gives each its class and owner):
/// no file may join, no count may grow, a cleared file leaves.
/// </summary>
public sealed class HardCodedLimitRatchetTests
{
    private const string BaselineFile = "hard-coded-limits-baseline.tsv";

    private const string Remedy =
        "put the bound in configuration and name its value, its reason and its key in the message it "
        + "stops with, or mark a constant that bounds nothing '// not a limit: <why>'. Do not add a baseline row.";

    [Fact]
    public void Backend_ANewFile_HardCodesNoLimit()
    {
        var baseline = Baseline();
        var offenders = Counts().Where(c => !baseline.ContainsKey(c.Key)).Select(c => $"{c.Key}: {c.Value}").ToList();

        offenders.Should().BeEmpty("a new limit lives in config (rule 4): " + Remedy + "\n  " + string.Join("\n  ", offenders));
    }

    [Fact]
    public void Backend_NoListedCount_Grew()
    {
        var counts = Counts();
        var grown = Baseline()
            .Where(row => counts.TryGetValue(row.Key, out var now) && now > row.Value)
            .Select(row => $"{row.Key}: {row.Value} → {counts[row.Key]}")
            .ToList();

        grown.Should().BeEmpty("a listed file may only lose hard-coded limits: " + Remedy + "\n  " + string.Join("\n  ", grown));
    }

    [Fact]
    public void Backend_AClearedFile_MustLeaveTheBaseline()
    {
        var counts = Counts();
        var stale = Baseline().Keys.Where(k => !counts.ContainsKey(k)).ToList();

        stale.Should().BeEmpty($"a file without hard-coded limits (or gone) must leave {BaselineFile}.\n  "
            + string.Join("\n  ", stale));
    }

    internal static IReadOnlyDictionary<string, int> Counts() =>
        ArchitectureSources.HandWrittenBackendFiles()
            .Select(p => (Key: Path.GetRelativePath(ArchitectureSources.BackendRoot, p).Replace('\\', '/'),
                Count: HardCodedLimitScanner.Count(File.ReadAllText(p))))
            .Where(x => x.Count > 0)
            .ToDictionary(x => x.Key, x => x.Count, StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, int> Baseline() =>
        File.ReadAllLines(Path.Combine(ArchitectureSources.TestSourceRoot, "Architecture", BaselineFile))
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(line => line.Split('\t'))
            .ToDictionary(parts => parts[1], parts => int.Parse(parts[0]), StringComparer.Ordinal);
}
