using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// p0507: the record rules read a date-minted id in all three places they read a counter
/// id — a spec's <c>phase:</c> field, a context key, a <c>requires:</c> entry.
/// <para>
/// An id a rule cannot read is an id nobody checks: the duplicate rule stops seeing the
/// phase, the context key matches nothing, and a <c>requires:</c> naming it resolves to a
/// phase nobody wrote. The counter branch is untouched, so p0430's ratchet does not move.
/// </para>
/// </summary>
public sealed class PhaseIdMintingTests
{
    private const string Minted = "2026-08-24-8a3f";

    [Fact]
    public void PhaseRecord_DateMintedPhase_IsCountedForDuplicates() =>
        SpecId($"phase: {Minted}\ngoal: \"anything\"\n").Should().Be(Minted);

    /// <summary>
    /// The id is the fixed-width prefix, so two files whose names differ only by slug are
    /// two files claiming ONE id — which is exactly what the duplicate rule must catch.
    /// </summary>
    [Fact]
    public void PhaseRecord_TwoFilesClaimingOneDateMintedId_GoesRed() =>
        SpecId($"phase: {Minted}-first\n").Should().Be(SpecId($"phase: {Minted}-second\n"));

    [Fact]
    public void PhaseRecord_RequiresNamingADateMintedPhase_IsCheckedForExistence()
    {
        PhaseIdReader.Current.Requires($"requires: [\"{Minted}\"]\n").Should().Equal(Minted);
        PhaseIdReader.Current.Requires($"requires:\n  - {Minted}\n").Should().Equal(Minted);
    }

    [Fact]
    public void PhaseRecord_DateMintedDonePhase_MustAppearInTheContext()
    {
        var match = PhaseIdReader.Current.ContextId.Match($"    {Minted}: \"what shipped\"\n");

        match.Success.Should().BeTrue("state.done keys the record by the minted id");
        match.Groups["id"].Value.Should().Be(Minted);
    }

    /// <summary>
    /// A counter id read the way it was always read — the widening is additive, and this
    /// is the assertion that says so at the level the record rules use.
    /// </summary>
    [Fact]
    public void PhaseRecord_CounterShapedPhase_ReadsExactlyAsBefore()
    {
        SpecId("phase: p0507\n").Should().Be("p0507");
        SpecId("phase: p19106a\n").Should().Be("p19106a");
        SpecId("phase: p0169j-a-frozen-trail-persistence\n").Should().Be("p0169j");
    }

    private static string SpecId(string specText) =>
        PhaseIdReader.Current.SpecId.Match(specText).Groups["id"].Value;
}
