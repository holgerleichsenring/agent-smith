using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// 2026-09-22-3f7c: a test says WHAT it waits for, never HOW LONG.
/// <para>
/// Three pull requests in two days were failed by one test out of roughly seven thousand,
/// a different one each time, each after about a minute — on a runner where copying a single
/// file into a workspace took seventy-eight seconds. Every answer until now was a bigger
/// number: one second became thirty, thirty became a hundred and twenty, ten became sixty.
/// A longer deadline on a slower runner is the same defect with a bigger number, and it
/// costs that much more every time the test passes.
/// </para>
/// <para>
/// So the deadlines are gone. A wait is on the EVENT the code under test raises, or it is a
/// poll of the state under <see cref="TestHelpers.TestWaits.Hang"/> — one ceiling for the
/// whole suite, which says "hung", not "slow", and is never tuned to make anything pass. The
/// baseline lists the places where the clock is genuinely the subject, and it is a ratchet:
/// a listed file may only lose entries, a fixed file must leave the list, and no unlisted
/// file may gain one.
/// </para>
/// </summary>
public sealed class WallClockRuleTests
{
    private const string BaselineFile = "wall-clock-baseline.tsv";

    [Fact]
    public void Suite_NoAssertionOutsideTheLabelledCategory_DependsOnWallClockTime()
    {
        var baseline = Baseline();
        var offenders = WallClockWaits.Counted()
            .Where(f => !baseline.ContainsKey(f.Key))
            .Select(f => $"{f.Key} ({f.Value})")
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        offenders.Should().BeEmpty(
            "a test waits on what the code signals, or polls the state under TestWaits.Hang. "
            + "Do not add a deadline and do not add a baseline entry — the entries are the "
            + "places where the clock is the subject.\n  " + string.Join("\n  ", offenders));
    }

    [Fact]
    public void NoBaselineFile_GainedAWallClockWaitSinceTheRuleWasInstalled()
    {
        var counted = WallClockWaits.Counted();

        var grown = Baseline()
            .Where(entry => counted.TryGetValue(entry.Key, out var now) && now > entry.Value)
            .Select(entry => $"{entry.Key}: {entry.Value} → {counted[entry.Key]}")
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        grown.Should().BeEmpty(
            "a file where the clock is already the subject may only lose waits, never gain "
            + "one.\n  " + string.Join("\n  ", grown));
    }

    [Fact]
    public void NoFixedFile_StillSitsInTheWallClockBaseline()
    {
        var counted = WallClockWaits.Counted();

        var stale = Baseline()
            .Where(entry => !counted.TryGetValue(entry.Key, out var now) || now < entry.Value)
            .Select(entry => entry.Key)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        stale.Should().BeEmpty(
            "a file that lost a wall-clock wait must say so here, so the list keeps telling "
            + "the truth about what is left.\n  " + string.Join("\n  ", stale));
    }

    private static IReadOnlyDictionary<string, int> Baseline()
    {
        var path = Path.Combine(ArchitectureSources.TestSourceRoot, "Architecture", BaselineFile);
        return File.ReadAllLines(path)
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .Select(l => l.Split('\t'))
            .ToDictionary(parts => parts[1], parts => int.Parse(parts[0]), StringComparer.Ordinal);
    }
}
