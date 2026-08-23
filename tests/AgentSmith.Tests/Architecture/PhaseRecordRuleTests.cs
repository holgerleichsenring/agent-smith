using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// p0430: the phase record states what the repository actually did, enforced rather
/// than remembered.
/// <para>
/// Steps 8 and 9 of the workflow — update the context, move the phase to done/ — are the
/// ones that get skipped, every time, because they come after the work is already proven.
/// On 2026-08-17 that had left nine shipped phases in active/, eleven phases missing from
/// the context, two phase ids resolving to two files each, and a <c>requires: p0422</c>
/// pointing at a spec nobody ever wrote. A rule nobody can forget beats a rule everyone
/// agrees with.
/// </para>
/// <para>
/// The 400 phases already on the record do not get an exemption — they get p0403's
/// RATCHET. The baseline lists every violation as it stood when the rule was installed;
/// from here nothing new may join it, and an entry that is fixed must leave it. Writing
/// a plausible history for a phase from 2026-03 would be invention, which is worse than
/// a named gap; the ratchet keeps the gap named and the debt one-directional.
/// </para>
/// </summary>
public sealed class PhaseRecordRuleTests
{
    private const string BaselineFile = "phase-record-baseline.tsv";
    private const string WriteFlag = "AGENTSMITH_WRITE_PHASE_BASELINE";

    private static readonly PhaseRecord Record = PhaseRecord.Current;

    [Fact]
    public void PhaseRecord_EveryPhaseId_ResolvesToOneFile() =>
        Assert("duplicate-id", Record.DuplicateIds(),
            "a phase id that resolves to two files makes every `requires:` naming it "
            + "ambiguous. Renumber the one that has not shipped.");

    [Fact]
    public void PhaseRecord_EveryDonePhase_IsRecordedInTheContext() =>
        Assert("unrecorded", Record.UnrecordedDonePhases(),
            "a phase in done/ has shipped, and the context is where the next agent reads "
            + "what shipped. Add it to state.done in contexts/default/context.yaml.");

    [Fact]
    public void PhaseRecord_EveryRequires_NamesAPhaseThatExists() =>
        Assert("dangling", Record.DanglingRequires(),
            "a `requires:` naming a phase nobody wrote is a dependency on a document that "
            + "does not exist. Write the spec, or drop the reference.");

    /// <summary>
    /// A phase cannot be both shipped and upcoming. No baseline: the overlap was two
    /// entries, both fixed in p0430, so there is no debt to ratchet.
    /// </summary>
    [Fact]
    public void PhaseRecord_NoShippedPhase_IsStillListedAsPlanned()
    {
        var stale = Record.ContextIdsUnder("planned")
            .Intersect(Record.ContextIdsUnder("done"), StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        // Says "these two facts cannot both hold", never "someone made an error" — this
        // rule fires most often on a MERGE, where each side was individually correct and
        // the overlap exists only in the join. A message that named a culprit would send
        // the reader hunting one side for a mistake that is not there.
        stale.Should().BeEmpty(
            "a phase cannot be both shipped and upcoming. Both entries may be correct on "
            + "their own branch — if this appeared in a merge, the shipped side wins and "
            + "the planned entry goes (additions merge, deletions win).\n  "
            + string.Join("\n  ", stale));
    }

    /// <summary>
    /// The ratchet: nothing new may be added, and nothing fixed may stay. Regenerate with
    /// <c>AGENTSMITH_WRITE_PHASE_BASELINE=1</c> only when the baseline SHRINKS.
    /// </summary>
    private static void Assert(string kind, IReadOnlyCollection<string> violations, string because)
    {
        if (Environment.GetEnvironmentVariable(WriteFlag) == "1") { Rewrite(); return; }

        var baseline = Baseline(kind);
        var added = violations.Where(v => !baseline.Contains(v))
            .OrderBy(x => x, StringComparer.Ordinal).ToList();
        added.Should().BeEmpty(
            $"{because}\n  " + string.Join("\n  ", added));

        var fixedEntries = baseline.Except(violations, StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal).ToList();
        fixedEntries.Should().BeEmpty(
            $"the debt only moves one way — remove the fixed '{kind}' entries from "
            + $"{BaselineFile}.\n  " + string.Join("\n  ", fixedEntries));
    }

    private static void Rewrite()
    {
        var rows = Record.DuplicateIds().Select(v => $"duplicate-id\t{v}")
            .Concat(Record.UnrecordedDonePhases().Select(v => $"unrecorded\t{v}"))
            .Concat(Record.DanglingRequires().Select(v => $"dangling\t{v}"))
            .OrderBy(x => x, StringComparer.Ordinal);
        File.WriteAllText(BaselinePath(), string.Join("\n", rows) + "\n");
    }

    private static HashSet<string> Baseline(string kind) =>
        File.ReadAllLines(BaselinePath())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.Split('\t', 2))
            .Where(parts => parts.Length == 2 && parts[0] == kind)
            .Select(parts => parts[1])
            .ToHashSet(StringComparer.Ordinal);

    private static string BaselinePath() =>
        Path.Combine(ArchitectureSources.TestSourceRoot, "Architecture", BaselineFile);
}
