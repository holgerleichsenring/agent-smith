using AgentSmith.Application.Services.PhaseExecution;
using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// p0512: a phase record entry is at most 400 characters — an index line, not an essay.
/// <para>
/// The record is the first thing every agent reads, and it had been growing without a
/// limit: the mean entry ran 100 characters over p0000-p0099 and 3039 over p0500-p0599,
/// thirtyfold, still accelerating. The operator asked twice for shorter entries and both
/// times the request died against the next phase. A number that fails a build survives.
/// </para>
/// <para>
/// 400 is read off the record rather than invented — half the entries already fit it. It
/// is about four sentences: what shipped, in what area, and the <c>-> phases/done/…</c>
/// pointer that every entry already carries. The detail is not lost by shortening, it is
/// one file away in the spec and its decisions, and an entry repeating its spec is a
/// second copy that will eventually disagree with the first.
/// </para>
/// <para>
/// The entries already over the cap get p0403's RATCHET, not an exemption: each is
/// listed at the length it had, may only get SHORTER, must LEAVE the list once it fits,
/// and nothing new may join. The debt stays visible and moves one direction.
/// </para>
/// </summary>
public sealed class PhaseRecordLengthRatchetTests
{
    // 2026-08-26-31e5: the number lives with the writer that composes every line to fit it.
    // A second literal here would be a cap this repository enforces and the product does not.
    private const int MaxChars = PhaseRecordIndexLine.MaxChars;
    private const int CompressionCeiling = 3000;
    private const string BaselineFile = "phase-record-length-baseline.tsv";

    [Fact]
    public void PhaseRecord_ANewEntry_FitsTheCap()
    {
        var baseline = Baseline();
        var offenders = Entries()
            .Where(e => e.Entry.Length > MaxChars && !baseline.ContainsKey(e.PhaseId))
            .Select(e => $"{e.PhaseId}: {e.Entry.Length} chars")
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        offenders.Should().BeEmpty(
            $"a phase record entry is an index line, max {MaxChars} characters: what "
            + "shipped, in what area, and the pointer into phases/done/. The reasoning "
            + "belongs in the spec the pointer names — do not add a baseline entry.\n  "
            + string.Join("\n  ", offenders));
    }

    [Fact]
    public void PhaseRecord_NoEntryGrew_SinceTheBaselineWasTaken()
    {
        var byId = ByPhaseId();
        var grown = Baseline()
            .Where(row => byId.TryGetValue(row.Key, out var now) && now > row.Value)
            .Select(row => $"{row.Key}: {row.Value} → {byId[row.Key]} chars")
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        grown.Should().BeEmpty(
            "an entry already over the cap may only get shorter. Whatever you were about "
            + "to append belongs in its phase spec.\n  " + string.Join("\n  ", grown));
    }

    [Fact]
    public void PhaseRecord_AnEntryThatShrankUnderTheCap_MustLeaveTheBaseline()
    {
        var byId = ByPhaseId();
        var stale = Baseline()
            .Where(row => !byId.TryGetValue(row.Key, out var now) || now <= MaxChars)
            .Select(row => row.Key)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        stale.Should().BeEmpty(
            $"an entry that now fits the cap (or is gone) must leave {BaselineFile}, so "
            + "the list keeps telling the truth about what is left.\n  "
            + string.Join("\n  ", stale));
    }

    /// <summary>
    /// The floor under the debt, which no baseline row may lift. Eleven entries held
    /// roughly 34,000 characters between them and were compressed to the cap in this
    /// phase; the ratchet alone would let a future regeneration put an essay back, so
    /// the ceiling is asserted against every entry with no exemption available.
    /// </summary>
    [Fact]
    public void PhaseRecord_TheElevenLongest_AreUnderTheCap()
    {
        var essays = Entries()
            .Where(e => e.Entry.Length > CompressionCeiling)
            .Select(e => $"{e.PhaseId}: {e.Entry.Length} chars")
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        essays.Should().BeEmpty(
            $"no entry may exceed {CompressionCeiling} characters, baselined or not — "
            + "the eleven that did were compressed and nothing may take their place.\n  "
            + string.Join("\n  ", essays));
    }

    private static IReadOnlyDictionary<string, int> Baseline() =>
        File.ReadAllLines(Path.Combine(
                ArchitectureSources.TestSourceRoot, "Architecture", BaselineFile))
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(line => line.Split('\t'))
            .ToDictionary(parts => parts[1], parts => int.Parse(parts[0]), StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, int> ByPhaseId() =>
        Entries().ToDictionary(e => e.PhaseId, e => e.Entry.Length, StringComparer.Ordinal);

    private static IReadOnlyList<(string PhaseId, string Entry)> Entries() =>
        PhaseRecordFile.DoneEntries();
}
