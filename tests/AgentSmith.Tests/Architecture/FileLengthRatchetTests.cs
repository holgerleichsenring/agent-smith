using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// p0402: max 120 lines per file — the coding principles' hard limit, made
/// enforceable. One type per file means a file's length IS its class's length,
/// and a class nobody can hold in their head is a class nobody can change safely.
/// <para>
/// The limit is not negotiable, so the 187 files that already exceeded it do not
/// get an exemption — they get a RATCHET. The baseline records each file's length
/// at the moment the rule was installed; from here a listed file may only get
/// shorter, no new file may join the list, and a file that drops to the limit must
/// leave it. The debt can only move one direction.
/// </para>
/// </summary>
public sealed class FileLengthRatchetTests
{
    private const int MaxLines = 120;
    private const string BaselineFile = "file-length-baseline.tsv";

    [Fact]
    public void NoNewFile_ExceedsTheLineLimit()
    {
        var baseline = Baseline();
        var newOffenders = Sources()
            .Where(f => f.Lines > MaxLines && !baseline.ContainsKey(f.Path))
            .Select(f => $"{f.Path} ({f.Lines} lines)")
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        newOffenders.Should().BeEmpty(
            $"max {MaxLines} lines per file. Split the responsibilities — do not add a "
            + $"baseline entry.\n  " + string.Join("\n  ", newOffenders));
    }

    [Fact]
    public void NoBaselineFile_GrewSinceTheRatchetWasInstalled()
    {
        var baseline = Baseline();
        var byPath = Sources().ToDictionary(f => f.Path, f => f.Lines, StringComparer.Ordinal);

        var grown = baseline
            .Where(entry => byPath.TryGetValue(entry.Key, out var now) && now > entry.Value)
            .Select(entry => $"{entry.Key}: {entry.Value} → {byPath[entry.Key]} lines")
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        grown.Should().BeEmpty(
            "a file already over the limit may only get shorter. Extract the "
            + "responsibility you were about to add.\n  " + string.Join("\n  ", grown));
    }

    [Fact]
    public void NoFixedFile_StillSitsInTheBaseline()
    {
        var byPath = Sources().ToDictionary(f => f.Path, f => f.Lines, StringComparer.Ordinal);

        var stale = Baseline()
            .Where(entry => !byPath.TryGetValue(entry.Key, out var now) || now <= MaxLines)
            .Select(entry => entry.Key)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        stale.Should().BeEmpty(
            "a file that now meets the limit (or is gone) must leave the baseline, so "
            + "the list keeps telling the truth about what is left.\n  "
            + string.Join("\n  ", stale));
    }

    private static IReadOnlyDictionary<string, int> Baseline()
    {
        var path = Path.Combine(ArchitectureSources.TestSourceRoot, "Architecture", BaselineFile);
        return File.ReadAllLines(path)
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .Select(l => l.Split('\t'))
            .ToDictionary(parts => parts[1], parts => int.Parse(parts[0]), StringComparer.Ordinal);
    }

    private static IReadOnlyList<(string Path, int Lines)> Sources() =>
        [.. ArchitectureSources.HandWrittenBackendFiles()
            .Select(f => (
                Path: Path.GetRelativePath(ArchitectureSources.BackendRoot, f).Replace('\\', '/'),
                Lines: File.ReadLines(f).Count()))];
}
