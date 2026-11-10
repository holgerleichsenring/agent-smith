using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using FluentAssertions;
using static AgentSmith.Tests.SpecDialog.ProposalReview.ProposalReviewRig;

namespace AgentSmith.Tests.SpecDialog.ProposalReview;

/// <summary>2026-11-09-9631b: a draft nobody could read is not reviewed, says so, and holds the
/// approval for one further turn; a reviewer not taken for another reason changes nothing.</summary>
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
        rig.Outcome.Findings.Should().Contain(f => f.SpecId == First && f.Why == ProposalReviewSummary.UnreadWhy);
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
    public async Task ProposalReviewLoop_BlindAgainOnTheTurnThatWaitedUnread_LiftsTheHoldAndKeepsTheFinding()
    {
        var rig = new ProposalReviewRig { OnReview = input => input.Draft.SpecId == Second ? Blind() : Build() };
        var earlier = rig.Propose(Epic());
        rig.Pipeline.Set(ContextKeys.SpecDialogRevisedProposal, earlier with
        {
            NotReviewed = [Second],
            Findings = [new ProposalFinding(Second, ProposalReviewSummary.NotReviewedYet, ProposalReviewSummary.UnreadWhy)],
        });

        await rig.RunAsync();

        rig.Outcome.NotReviewed.Should().BeNull("a repository that can never be read must not hold the approval for good");
        rig.Outcome.Findings.Should().Contain(f => f.SpecId == Second && f.Why == ProposalReviewSummary.UnreadAgainWhy);
    }

    [Fact]
    public async Task ProposalReviewLoop_BlindOnTheTurnAfterABudgetStop_StillHolds()
    {
        var rig = new ProposalReviewRig { OnReview = input => input.Draft.SpecId == Second ? Blind() : Build() };
        var earlier = rig.Propose(Epic());
        rig.Pipeline.Set(ContextKeys.SpecDialogRevisedProposal, earlier with
        {
            NotReviewed = [Second],
            Findings = [new ProposalFinding(Second, ProposalReviewSummary.NotReviewedYet,
                ProposalReviewSummary.NotReviewedWhy("the run's cost cap is reached"))],
        });

        await rig.RunAsync();

        rig.Outcome.NotReviewed.Should().Equal([Second], "waiting on the budget is not a try at reading; the blind exit needs one");
        rig.Outcome.Findings.Should().Contain(f => f.SpecId == Second && f.Why == ProposalReviewSummary.UnreadWhy);
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
