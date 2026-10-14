using System.Text;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using FluentAssertions;
using static AgentSmith.Tests.SpecDialog.TranscriptDigest.DigestTurns;

namespace AgentSmith.Tests.SpecDialog.TranscriptDigest;

/// <summary>2026-10-14-dbceo: what a dialog turn's prompt carries of a long conversation, and when a fold is due.</summary>
public sealed class TranscriptWindowTests
{
    /// <summary>The rendering every dialog prompt carried before the window: every turn, oldest first.</summary>
    private static string Today(IEnumerable<SpecDialogTurn> turns)
    {
        var sb = new StringBuilder();
        foreach (var turn in turns)
            sb.AppendLine(turn.Author is { } author ? $"[{turn.Role}: {author}] {turn.Text}" : $"[{turn.Role}] {turn.Text}");
        return sb.ToString().TrimEnd();
    }

    [Fact]
    public void TranscriptWindow_AtMost60000Chars_RendersExactlyTodaysTranscript()
    {
        IReadOnlyList<SpecDialogTurn> shortOne = [U("need a widget", "Ada"), A("which ledger?  "), U("the main one  ")];
        IReadOnlyList<SpecDialogTurn> longWithoutDigest = [U("need a widget"), A(Big("A", 40_000)), U(Big("B", 30_000))];
        IReadOnlyList<SpecDialogTurn> withDigestUnderTheBound = [U("need a widget"), A("short"), Digest("tiny digest", 1)];

        TranscriptWindow.Render(shortOne).Should().Be(Today(shortOne));
        TranscriptWindow.Render(longWithoutDigest).Should().Be(Today(longWithoutDigest));
        TranscriptWindow.Render(withDigestUnderTheBound).Should().Be(Today(withDigestUnderTheBound));
        DialogTranscriptSection.Render(shortOne).Should().Be(Today(shortOne));
    }

    [Fact]
    public void TranscriptWindow_PastTheBoundWithADigest_RendersDigestThenPinnedThenEveryLaterTurn()
    {
        var rendered = TranscriptWindow.Render(
        [
            U("require the ledger widget"), A(Big("FOLDED-1", 25_000)), U(Big("FOLDED-2", 20_000)),
            A(Big("FOLDED-3", 20_000)), U("FOLDED-4 more"), Digest("DIGEST-TEXT", 4), A("recent answer"), U("recent question"),
        ]);

        rendered.Should().StartWith("[Turns 1 to 5 are summarised in the digest below;")
            .And.NotContain("FOLDED-");
        var order = new[] { "DIGEST-TEXT", "[user] require the ledger widget", "[assistant] recent answer", "[user] recent question" }
            .Select(part => rendered.IndexOf(part, StringComparison.Ordinal)).ToList();
        order.Should().NotContain(-1).And.BeInAscendingOrder();
        rendered.Should().EndWith("[user] recent question");
    }

    [Fact]
    public void TranscriptWindow_Pins_FilingMockRatifiedNewestProposalAndTheAnswerAfterAProposal()
    {
        var rendered = TranscriptWindow.Render(
        [
            U("open"), A(Big("FOLDED-a", 30_000)), A("proposal one", SpecDialogTurnKind.SpecProposal), U("approve one"),
            A("filed one", SpecDialogTurnKind.Filing), A("mock ok", SpecDialogTurnKind.MockRatified), A(Big("FOLDED-b", 30_000)),
            A("proposal two", SpecDialogTurnKind.TicketGraphProposal), U("edit note"), A(Big("FOLDED-c", 5_000)),
            Digest("DIGEST", 9), A("later"),
        ]);

        rendered.Should().Contain("[user] approve one").And.Contain("[assistant] filed one").And.Contain("[assistant] mock ok")
            .And.Contain("[assistant] proposal two").And.Contain("[user] edit note").And.Contain("[assistant] later");
        rendered.Should().NotContain("proposal one", "only the newest proposal stays verbatim").And.NotContain("FOLDED-");
    }

    [Fact]
    public void TranscriptWindow_Pins_TheOpeningOperatorTurn()
    {
        var rendered = TranscriptWindow.Render(
        [
            U("the requirement statement"), A(Big("FOLDED-1", 35_000)), U("second operator turn"), A(Big("FOLDED-2", 35_000)),
            Digest("DIGEST", 3), U("now"),
        ]);

        rendered.Should().Contain("[user] the requirement statement").And.NotContain("second operator turn");
    }

    [Fact]
    public void TranscriptWindow_DigestTurns_AreNeverRenderedAsTurns()
    {
        var rendered = TranscriptWindow.Render(
        [
            U("open"), A(Big("FOLDED-1", 35_000)), U("q"), Digest("OLD-DIGEST", 2), A(Big("FOLDED-2", 35_000)), U("q2"),
            Digest("NEW-DIGEST", 5), A("latest"),
        ]);

        rendered.Should().NotContain("OLD-DIGEST").And.NotContain("[assistant: agent-smith]");
        rendered.Split("NEW-DIGEST").Should().HaveCount(2, "the latest digest is rendered once, as the digest");
    }

    [Fact]
    public void TranscriptWindow_PersonTurnsWithAnAuthor_KeepTheNameVerbatimAndPinned()
    {
        var rendered = TranscriptWindow.Render(
        [
            U("need the widget", "Ada"), A(Big("FOLDED-1", 35_000)), A("the proposal", SpecDialogTurnKind.SpecProposal),
            U("approved", "Bob"), A(Big("FOLDED-2", 35_000)), Digest("DIGEST", 4), U("latest word", "Cy"),
        ]);

        rendered.Should().Contain("[user: Ada] need the widget").And.Contain("[user: Bob] approved")
            .And.Contain("[user: Cy] latest word");
    }

    [Fact]
    public void TranscriptWindow_Fold_TailAboveBound_FoldsUntilTheRestIsAtMost30000Chars()
    {
        var turns = Enumerable.Range(0, 10).Select(i => i % 2 == 0 ? U(Big($"T{i}", 9_000)) : A(Big($"T{i}", 9_000))).ToList();

        var fold = TranscriptWindow.Fold(turns)!;

        fold.From.Should().Be(0);
        fold.Through.Should().Be(6);
        fold.Turns.Should().Equal(turns.Take(7));
        fold.PreviousDigest.Should().BeNull();
        Width(turns.Skip(7)).Should().BeLessThanOrEqualTo(TranscriptWindow.KeepRecentChars);
        Width(turns.Skip(6)).Should().BeGreaterThan(TranscriptWindow.KeepRecentChars, "it folds no more than it has to");
    }

    [Fact]
    public void TranscriptWindow_Fold_TwoHugeNewestTurns_FoldsNeitherOfThem()
    {
        var fold = TranscriptWindow.Fold([U("a"), A(Big("H1", 40_000)), U(Big("H2", 40_000))])!;

        fold.Through.Should().Be(0);
        fold.Turns.Should().ContainSingle();
        TranscriptWindow.Fold([A(Big("H1", 40_000)), U(Big("H2", 40_000))]).Should().BeNull();
    }

    [Fact]
    public void TranscriptWindow_Fold_RightAfterAFold_IsNullUntilTheTailPassesTheBoundAgain()
    {
        var turns = Enumerable.Range(0, 10).Select(i => U(Big($"T{i}", 9_000))).ToList();
        var first = TranscriptWindow.Fold(turns)!;
        turns.Add(Digest("THE-DIGEST", first.Through));

        TranscriptWindow.Fold(turns).Should().BeNull("the tail was folded down to 30,000 characters");
        turns.AddRange(Enumerable.Range(0, 3).Select(i => A(Big($"N{i}", 9_000))));
        TranscriptWindow.Fold(turns).Should().BeNull("about 54,000 characters of tail are still under the bound");
        turns.Add(U(Big("N3", 9_000)));

        var next = TranscriptWindow.Fold(turns)!;
        next.From.Should().Be(first.Through + 1);
        next.PreviousDigest.Should().Be("THE-DIGEST");
        next.Turns.Should().NotContain(t => t.Kind == SpecDialogTurnKind.Digest);
    }

    [Fact]
    public void TranscriptWindow_Fold_InputAboveMaxFoldChars_StopsEarlyAndTheNextFoldContinues()
    {
        var turns = Enumerable.Range(0, 30).Select(i => U(Big($"T{i}", 9_000))).ToList();

        var first = TranscriptWindow.Fold(turns)!;
        Width(first.Turns).Should().BeLessThanOrEqualTo(TranscriptWindow.MaxFoldChars);
        Width(turns.Skip(first.Through + 1)).Should().BeGreaterThan(TranscriptWindow.VerbatimChars);
        turns.Add(Digest("D1", first.Through));

        var next = TranscriptWindow.Fold(turns)!;
        next.From.Should().Be(first.Through + 1);
        Width(turns.Skip(next.Through + 1).Where(t => t.Kind != SpecDialogTurnKind.Digest))
            .Should().BeLessThanOrEqualTo(TranscriptWindow.KeepRecentChars);
    }
}
