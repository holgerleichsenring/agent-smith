using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// p0509: a spec id a rule cannot read is a spec id nobody checks.
/// <para>
/// The framework mints <c>p19106a</c> from ticket 19106 and writes it into every target
/// repository as a spec file. Read four digits at a time it becomes <c>p1910</c>: two
/// specs of one ticket collapse onto one id, the duplicate-id rule sees a clash that is
/// not one, the context key matches nothing, and a <c>requires:</c> naming it resolves to
/// a spec nobody wrote.
/// </para>
/// </summary>
public sealed class SpecIdExtractionTests
{
    [Fact]
    public void SpecId_TicketDerivedId_IsExtractedWhole()
    {
        SpecId("spec: p19106a\ngoal: \"anything\"\n").Should().Be("p19106a");

        // The same widening is what makes the house's own `-pre` convention usable again.
        SpecId("spec: p0503a-pre\ngoal: \"anything\"\n").Should().Be("p0503a-pre");
    }

    [Fact]
    public void SpecId_TwoSpecsFromOneTicket_AreTwoIds() =>
        SpecId("spec: p19106a\n").Should().NotBe(SpecId("spec: p19106b\n"));

    [Fact]
    public void SpecId_ContextKeyForATicketDerivedId_IsMatched()
    {
        var match = SpecIdReader.Current.ContextId.Match("    p19106a: \"what shipped\"\n");

        match.Success.Should().BeTrue("state.done keys the record by the minted id");
        match.Groups["id"].Value.Should().Be("p19106a");
    }

    /// <summary>
    /// The dangling rule looks the required id up among the ids it knows, so the lookup is
    /// only as good as the reading. Both spellings the specs use are covered.
    /// </summary>
    [Fact]
    public void SpecId_RequiresNamingATicketDerivedId_IsCheckedForExistence()
    {
        SpecIdReader.Current.Requires("requires: [\"p19106a\"]\n").Should().Equal("p19106a");
        SpecIdReader.Current.Requires("requires:\n  - p19106a\n").Should().Equal("p19106a");
    }

    /// <summary>
    /// p0430's ratchet fails in BOTH directions, so a change to the reading is a change to
    /// the debt. Every spec, context key and <c>requires:</c> entry this repository holds
    /// is read twice — once the way it was read before, once the way it is read now — and
    /// the two must agree apart from the departures recorded here.
    /// </summary>
    [Fact]
    public void SpecRecord_ExistingSpecs_ProduceTheSameViolationSetAsBefore()
    {
        var before = new SpecRecord(SpecIdReader.Legacy);
        var after = SpecRecord.Current;

        Departures(before.DuplicateIds(), after.DuplicateIds())
            .Should().BeEquivalentTo(["-p0131c"],
                "p0131c-pre is a spec in its own right — schema-legal, shipped, and named "
                + "by p0131c's own `requires:`. It counted as a duplicate only because the "
                + "reading stopped before its tail, so it leaves the baseline.");
        Departures(before.UnrecordedDoneSpecs(), after.UnrecordedDoneSpecs()).Should().BeEmpty();
        Departures(before.DanglingRequires(), after.DanglingRequires()).Should().BeEmpty();
        Departures(Overlap(before), Overlap(after)).Should().BeEmpty();
    }

    /// <summary>
    /// p0507's landmine, found on the first use of what p0507 shipped. The legacy comparison
    /// reads EVERY spec on disk with the pre-p0509 four-digit reading and insists each one
    /// matches — so the first date-minted spec anyone wrote turned it red. A reading that is
    /// historical must skip what it could never have named; the reading in force must not.
    /// </summary>
    [Fact]
    public void LegacyReading_ADateMintedSpec_IsSkippedRatherThanCalledUndeclared()
    {
        const string minted = "spec: 2026-08-24-035a\n";

        SpecIdReader.Legacy.ReadsEveryNamespace.Should().BeFalse(
            "a spec the four-digit reading cannot name did not exist while it was the rule");
        SpecIdReader.Legacy.SpecId.Match(minted).Success.Should().BeFalse();

        SpecIdReader.Current.ReadsEveryNamespace.Should().BeTrue(
            "the reading in force names both namespaces, so a spec it cannot read declares nothing");
        SpecIdReader.Current.SpecId.Match(minted).Groups["id"].Value.Should().Be("2026-08-24-035a");
    }

    private static string SpecId(string specText) =>
        SpecIdReader.Current.SpecId.Match(specText).Groups["id"].Value;

    private static IEnumerable<string> Overlap(SpecRecord record) =>
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
