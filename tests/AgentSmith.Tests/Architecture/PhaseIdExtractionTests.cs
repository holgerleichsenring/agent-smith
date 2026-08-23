using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// p0509: a phase id a rule cannot read is a phase id nobody checks.
/// <para>
/// The framework mints <c>p19106a</c> from ticket 19106 and writes it into every target
/// repository as a phase file. Read four digits at a time it becomes <c>p1910</c>: two
/// phases of one ticket collapse onto one id, the duplicate-id rule sees a clash that is
/// not one, the context key matches nothing, and a <c>requires:</c> naming it resolves to
/// a phase nobody wrote.
/// </para>
/// </summary>
public sealed class PhaseIdExtractionTests
{
    [Fact]
    public void PhaseId_TicketDerivedId_IsExtractedWhole()
    {
        SpecId("phase: p19106a\ngoal: \"anything\"\n").Should().Be("p19106a");

        // The same widening is what makes the house's own `-pre` convention usable again.
        SpecId("phase: p0503a-pre\ngoal: \"anything\"\n").Should().Be("p0503a-pre");
    }

    [Fact]
    public void PhaseId_TwoPhasesFromOneTicket_AreTwoIds() =>
        SpecId("phase: p19106a\n").Should().NotBe(SpecId("phase: p19106b\n"));

    [Fact]
    public void PhaseId_ContextKeyForATicketDerivedId_IsMatched()
    {
        var match = PhaseIdReader.Current.ContextId.Match("    p19106a: \"what shipped\"\n");

        match.Success.Should().BeTrue("state.done keys the record by the minted id");
        match.Groups["id"].Value.Should().Be("p19106a");
    }

    /// <summary>
    /// The dangling rule looks the required id up among the ids it knows, so the lookup is
    /// only as good as the reading. Both spellings the specs use are covered.
    /// </summary>
    [Fact]
    public void PhaseId_RequiresNamingATicketDerivedId_IsCheckedForExistence()
    {
        PhaseIdReader.Current.Requires("requires: [\"p19106a\"]\n").Should().Equal("p19106a");
        PhaseIdReader.Current.Requires("requires:\n  - p19106a\n").Should().Equal("p19106a");
    }

    /// <summary>
    /// p0430's ratchet fails in BOTH directions, so a change to the reading is a change to
    /// the debt. Every spec, context key and <c>requires:</c> entry this repository holds
    /// is read twice — once the way it was read before, once the way it is read now — and
    /// the two must agree apart from the departures recorded here.
    /// </summary>
    [Fact]
    public void PhaseRecord_ExistingSpecs_ProduceTheSameViolationSetAsBefore()
    {
        var before = new PhaseRecord(PhaseIdReader.Legacy);
        var after = PhaseRecord.Current;

        Departures(before.DuplicateIds(), after.DuplicateIds())
            .Should().BeEquivalentTo(["-p0131c"],
                "p0131c-pre is a spec in its own right — schema-legal, shipped, and named "
                + "by p0131c's own `requires:`. It counted as a duplicate only because the "
                + "reading stopped before its tail, so it leaves the baseline.");
        Departures(before.UnrecordedDonePhases(), after.UnrecordedDonePhases()).Should().BeEmpty();
        Departures(before.DanglingRequires(), after.DanglingRequires()).Should().BeEmpty();
        Departures(Overlap(before), Overlap(after)).Should().BeEmpty();
    }

    private static string SpecId(string specText) =>
        PhaseIdReader.Current.SpecId.Match(specText).Groups["id"].Value;

    private static IEnumerable<string> Overlap(PhaseRecord record) =>
        record.ContextIdsUnder("planned")
            .Intersect(record.ContextIdsUnder("done"), StringComparer.Ordinal);

    private static IReadOnlyList<string> Departures(
        IEnumerable<string> before, IEnumerable<string> after)
    {
        var was = before.ToHashSet(StringComparer.Ordinal);
        var now = after.ToHashSet(StringComparer.Ordinal);
        return [.. was.Except(now).Select(v => $"-{v}")
            .Concat(now.Except(was).Select(v => $"+{v}"))
            .OrderBy(v => v, StringComparer.Ordinal)];
    }
}
