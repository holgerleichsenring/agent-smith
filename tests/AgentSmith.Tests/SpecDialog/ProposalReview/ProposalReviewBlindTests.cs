using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using FluentAssertions;
using static AgentSmith.Tests.SpecDialog.ProposalReview.ProposalReviewRig;

namespace AgentSmith.Tests.SpecDialog.ProposalReview;

/// <summary>2026-11-09-9631b: a draft nobody could read is not reviewed and says so. 2026-11-10-8e59: the mark is
/// information, every turn; the review before building reads it again.</summary>
public sealed class ProposalReviewBlindTests
{
    private static SpecAttackResult Blind() => new(SpecReviewVerdicts.Build, [], []) { Blind = true };

    [Fact]
    public async Task ProposalReviewLoop_AllReviewersBlind_DraftIsNotReviewedWithTheUnreadReason()
    {
        var rig = new ProposalReviewRig { OnReview = input => input.Draft.SpecId == First ? Blind() : Build() };
        rig.Propose(Epic());

        await rig.RunAsync();

        rig.Outcome.NotReviewed.Should().Equal(First);
        rig.Outcome.ReviewRounds.Should().NotContain(r => r.SpecId == First);
        rig.Outcome.Findings.Should().Contain(f => f.SpecId == First && f.Why == ProposalReviewSummary.NotReviewedWhy(ProposalReviewSummary.UnreadWhy));
    }

    [Fact]
    public async Task ProposalReviewLoop_OneReviewerBlind_TheOtherKeepsTheRoundTaken()
    {
        var rig = new ProposalReviewRig { OnReview = input => input.Reviewer == 0 ? Blind() : Build() };
        rig.Propose(Epic());

        await rig.RunAsync();

        rig.Outcome.NotReviewed.Should().BeNull();
        rig.Outcome.ReviewRounds.Should().Contain(r => r.SpecId == First
            && r.Verdict == SpecReviewVerdicts.Build && r.ReviewerVerdicts[0] == null);
    }

    [Fact]
    public async Task ProposalReviewLoop_BlindOnAWaitingDraft_IsMarkedAgainWithNoExit()
    {
        var rig = new ProposalReviewRig { OnReview = input => input.Draft.SpecId == Second ? Blind() : Build() };
        var earlier = rig.Propose(Epic());
        rig.Pipeline.Set(ContextKeys.SpecDialogRevisedProposal, earlier with
        {
            NotReviewed = [Second],
            Findings = [new ProposalFinding(Second, ProposalReviewSummary.NotReviewedYet,
                ProposalReviewSummary.NotReviewedWhy(ProposalReviewSummary.UnreadWhy))],
        });

        await rig.RunAsync();

        rig.Outcome.NotReviewed.Should().Equal([Second], "a mark is information; there is no hold to lift");
    }

    [Fact]
    public async Task ProposalReviewLoop_NoRepositoryToOpen_DoesNotHoldTheProposal()
    {
        var rig = new ProposalReviewRig { OnReview = _ => null };
        rig.Propose(Epic());

        await rig.RunAsync();

        rig.Outcome.NotReviewed.Should().BeNull();
    }
}
