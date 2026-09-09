using AgentSmith.Application.Services.Handlers;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Handlers;

// 2026-09-06-9f14: a disposition is paired with the criterion it names; position is the
// fallback PER DISPOSITION, never per array, so a name that matches nothing removes no
// pairing that position would have made.
public sealed class AcceptancePairingTests
{
    private const string A = "No dependency in backend/package.json remains on the removed library.";
    private const string B = "The frontend build passes with the replacement in place.";
    private const string C = "Existing callers keep their behaviour.";

    private static AcceptanceDisposition Met(string criterion) => new(criterion, AcceptanceStatus.Met, "an edit");

    private static AcceptanceDisposition Unmet(string criterion) => new(criterion, AcceptanceStatus.Unmet, "");

    private static IReadOnlyList<(string Criterion, string? Named, PairedBy By)> Shape(
        IReadOnlyList<CriterionPairing> pairings) =>
        pairings.Select(p => (p.Criterion, p.Disposition?.Criterion, p.By)).ToList();

    [Fact]
    public void Pairing_DispositionsInAnotherOrder_AreMatchedByCriterion() =>
        Shape(AcceptancePairing.Pair([A, B, C], [Met(C), Unmet(A), Met(B)])).Should().Equal(
            (A, A, PairedBy.Name), (B, B, PairedBy.Name), (C, C, PairedBy.Name));

    [Fact]
    public void Pairing_ALongerAnswerWithNames_PairsEveryCriterionAndDropsTheSurplus() =>
        Shape(AcceptancePairing.Pair([A, B], [Met(B), Met("Something the master invented."), Met(A)]))
            .Should().Equal((A, A, PairedBy.Name), (B, B, PairedBy.Name));

    [Fact]
    public void Pairing_ADispositionNamingNothing_StillPairsByItsPosition() =>
        Shape(AcceptancePairing.Pair([A, B, C], [Met(A), Met(""), Met("criterion 3")])).Should().Equal(
            (A, A, PairedBy.Name), (B, "", PairedBy.Position), (C, "criterion 3", PairedBy.Position));

    [Fact]
    public void Pairing_AnAnswerWhoseEveryNameMisses_PairsAsItDidBefore() =>
        Shape(AcceptancePairing.Pair([A, B], [Met("criterion 1"), Unmet("criterion 2")])).Should().Equal(
            (A, "criterion 1", PairedBy.Position), (B, "criterion 2", PairedBy.Position));

    [Theory]
    [InlineData("No dependency in backend/package.json remains on the removed library")]
    [InlineData("  No dependency in   backend/package.json remains\n on the removed library.  ")]
    [InlineData("no dependency in backend/package.json remains on the removed library.")]
    [InlineData("No dependency in backend/package.json remains on the removed library!")]
    public void Pairing_ADispositionWithTrailingPunctuationOrExtraSpace_StillMatches(string named) =>
        Shape(AcceptancePairing.Pair([B, A], [Met(named)])).Should().Equal(
            (B, null, PairedBy.Nothing), (A, named, PairedBy.Name));

    [Fact]
    public void Pairing_ADoneListRepeatingACriterion_PairsThemInOrderOfAppearance() =>
        Shape(AcceptancePairing.Pair([A, A, B], [Unmet(A), Met(A), Met(B)])).Should().Equal(
            (A, A, PairedBy.Name), (A, A, PairedBy.Name), (B, B, PairedBy.Name));

    [Fact]
    public void Pairing_ADoneListRepeatingACriterion_TheSecondDispositionTakesTheSecondSlot()
    {
        var pairings = AcceptancePairing.Pair([A, A], [Unmet(A), Met(A)]);

        pairings[0].Disposition!.Status.Should().Be(AcceptanceStatus.Unmet);
        pairings[1].Disposition!.Status.Should().Be(AcceptanceStatus.Met);
    }

    [Fact]
    public void Pairing_ANameAlreadySpent_FallsBackToItsPosition() =>
        Shape(AcceptancePairing.Pair([A, B], [Met(A), Met(A)])).Should().Equal(
            (A, A, PairedBy.Name), (B, A, PairedBy.Position));

    [Fact]
    public void Pairing_ACriterionWhoseSlotANameTook_GetsNothingFromPosition() =>
        Shape(AcceptancePairing.Pair([A, B], [Met(""), Met(A)])).Should().Equal(
            (A, A, PairedBy.Name), (B, null, PairedBy.Nothing));

    [Fact]
    public void Pairing_AnEmptyAnswer_LeavesEveryCriterionUnpaired() =>
        Shape(AcceptancePairing.Pair([A, B], [])).Should().Equal(
            (A, null, PairedBy.Nothing), (B, null, PairedBy.Nothing));

    [Theory]
    [InlineData("  Two   words. ", "two words")]
    [InlineData("Ends with a quote\"", "ends with a quote")]
    [InlineData("...", "")]
    [InlineData("", "")]
    public void Normalise_CollapsesWhitespaceDropsTrailingPunctuationAndCase(string text, string expected) =>
        AcceptancePairing.Normalise(text).Should().Be(expected);
}
