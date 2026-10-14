using AgentSmith.Contracts.Commands;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Server.Services.Tickets.Conversation;
using AgentSmith.Tests.Specs;
using AgentSmith.Tests.Tickets;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using static AgentSmith.Tests.SpecDialog.ProposalReview.ProposalReviewRig;

namespace AgentSmith.Tests.SpecDialog.ProposalReview;

/// <summary>
/// 2026-10-13-8762i: what the design partner is told after a round, and every surface a proposal's review rounds
/// and not-reviewed list travel through — the stored row, the pane push, the chat confirmation, the ticket page.
/// </summary>
public sealed class ProposalReviewSurfacesTests
{
    private static readonly SpecDraft Draft = new SpecDraftReader().Read(Yaml(Lone));

    private static readonly ProposalReviewRound Round = new(
        Lone, 1, SpecReviewVerdicts.BuildWithCuts, [SpecReviewVerdicts.BuildWithCuts, null],
        [Cut(SpecRegisterNames.Steps, "buffered", "streaming")],
        [new SpecReviewFinding("the writer streams", "shop/src/Writer.cs:9 [A1]")],
        [new SpecReviewResidue(SpecRegisterNames.Done, "buffered")]);

    private static OutcomeProposal Reviewed() => new SpecOutcome(Draft)
    {
        ReviewRounds = [Round],
        NotReviewed = [Lone, ProposalReviewTokens.Series],
        Findings = [new ProposalFinding(Lone, "not reviewed yet", "the share ran out")],
    };

    [Fact]
    public void ProposalCutsNudge_NamesRegisterQuoteReplacementAndEvidence()
    {
        var nudge = ProposalCutsNudge.For(
            [new ProposalNudgeItem(Draft, [Cut(SpecRegisterNames.Steps, "buffered", "streaming")], [], [])], [], lastRound: false);

        nudge.Should().Contain("register: steps").And.Contain("quote: \"buffered\"").And.Contain("replacement: \"streaming\"")
            .And.Contain("evidence: shop/src/Export/Writer.cs:9 [A1]").And.Contain(ProposalCutsNudge.ApplyExactly)
            .And.NotContain(ProposalCutsNudge.MoveRule);
    }

    [Fact]
    public void ProposalCutsNudge_LastRound_TellsTheMoveAndNamesRevertedDrafts()
    {
        var reverted = new SpecDraftReader().Read(Yaml(First));
        var nudge = ProposalCutsNudge.For(
            [new ProposalNudgeItem(Draft, [Cut(SpecRegisterNames.Steps, "buffered", null)], [],
                [new SpecEvidenceProblem("the store pages", "src/Store.cs has 3 lines")])],
            [reverted], lastRound: true);

        nudge.Should().Contain(ProposalCutsNudge.MoveRule).And.Contain("- the store pages: src/Store.cs has 3 lines")
            .And.Contain($"## {First} was reverted").And.Contain(reverted.Yaml.TrimEnd())
            .And.Contain("none — remove the quoted words");
    }

    [Fact]
    public void ForProposalAttack_NoTurnRepositories_IsNull() =>
        DerivationTestLooks.Factory().ForProposalAttack(new PipelineContext(), Draft).Should().BeNull();

    [Fact]
    public void OutcomeProposalJson_ReviewRoundsAndNotReviewed_RoundTrip_AndAnOldRowReadsNone()
    {
        var back = OutcomeProposalJson.Read(OutcomeProposalJson.Write(Reviewed()));
        back.ReviewRounds.Should().BeEquivalentTo([Round]);
        back.NotReviewed.Should().Equal(Lone, ProposalReviewTokens.Series);

        var old = OutcomeProposalJson.Read(OutcomeProposalJson.Write(new SpecOutcome(Draft)));
        old.ReviewRounds.Should().BeEmpty();
        old.NotReviewed.Should().BeNull();
        OutcomeProposalJson.Write(new SpecOutcome(Draft)).Should().NotContain("reviewRounds").And.NotContain("notReviewed");
    }

    [Fact]
    public void ProposalComposer_PushCarriesReviewRounds()
    {
        var push = new SpecDialogProposalComposer(new EpicChildOrderer(), new BugTicketRenderer())
            .Compose("d-1", Reviewed(), DateTimeOffset.UtcNow)!;

        push.ReviewRounds.Should().BeEquivalentTo([Round]);
        push.NotReviewed.Should().Equal(Lone, ProposalReviewTokens.Series);
    }

    [Fact]
    public void TicketTranscriptReader_ProposalCarriesFindingsRoundsAndNotReviewed()
    {
        var reader = new TicketTranscriptReader(Mock.Of<IServiceScopeFactory>(), NullLogger<TicketTranscriptReader>.Instance);

        var view = reader.Proposal(TicketConversationFixtures.Session(Reviewed()))!;

        view.Findings.Should().ContainSingle().Which.Problem.Should().Be("not reviewed yet");
        view.ReviewRounds.Should().BeEquivalentTo([Round]);
        view.NotReviewed.Should().Equal(Lone, ProposalReviewTokens.Series);
        reader.Proposal(TicketConversationFixtures.Session(new SpecOutcome(Draft)))!.NotReviewed.Should().BeNull();
    }

    [Fact]
    public void OutcomeComposer_NotReviewed_IsListedAmongTheFindings()
    {
        var text = new SpecDialogOutcomeComposer().ComposeConfirmation(Reviewed()).In(SpecDialogMarkup.For("slack"));

        text.Should().Contain("not reviewed yet");
    }
}
