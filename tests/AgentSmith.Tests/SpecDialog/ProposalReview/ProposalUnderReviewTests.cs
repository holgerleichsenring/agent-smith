using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Application.Services.Turns;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Turns;
using FluentAssertions;
using static AgentSmith.Tests.SpecDialog.ProposalReview.ProposalReviewRig;

namespace AgentSmith.Tests.SpecDialog.ProposalReview;

/// <summary>2026-11-09-9a98: what the master wrote is published before the review and after each round.</summary>
public sealed class ProposalUnderReviewTests
{
    private readonly AsyncLocalTurnProposalObserverAccessor _accessor = new();
    private readonly List<OutcomeProposal> _published = [];

    private sealed class Recording(List<OutcomeProposal> published) : ITurnProposalObserver
    {
        public Task PublishAsync(OutcomeProposal proposal, CancellationToken cancellationToken)
        {
            published.Add(proposal);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task ProposalReview_BeforeTheFirstRound_PublishesEveryDraftUnderReview()
    {
        var rig = new ProposalReviewRig { Proposals = _accessor };
        rig.OnReview = _ =>
        {
            _published.Should().NotBeEmpty("the proposal is out before any reviewer runs");
            return Build();
        };
        rig.Propose(Epic());
        using var observing = _accessor.Observe(new Recording(_published));

        await rig.RunAsync();

        _published[0].UnderReview.Should().BeTrue();
        _published[0].NotReviewed.Should().Equal(First, Second);
        _published[0].ReviewRounds.Should().BeEmpty();
        _published[0].Findings.Where(f => f.Problem == ProposalReviewSummary.NotReviewedYet).Select(f => f.SpecId)
            .Should().Equal([First, Second], "a turn cancelled from here leaves each draft's reason behind (2026-11-10-18cf)");
    }

    [Fact]
    public async Task ProposalReview_AfterARound_PublishesTheRoundAndWhatIsStillOpen()
    {
        var rig = new ProposalReviewRig { Proposals = _accessor };
        rig.OnReview = input => input.Draft.SpecId == First ? Build() : null;
        rig.Propose(Epic());
        using var observing = _accessor.Observe(new Recording(_published));

        await rig.RunAsync();

        var afterRound = _published[1];
        afterRound.UnderReview.Should().BeTrue();
        afterRound.ReviewRounds.Should().ContainSingle(r => r.SpecId == First && r.Verdict == SpecReviewVerdicts.Build);
    }

    [Fact]
    public async Task ProposalReview_WhenItEnds_TheFinalProposalCarriesNoMark()
    {
        var rig = new ProposalReviewRig { Proposals = _accessor };
        rig.Propose(Epic());
        using var observing = _accessor.Observe(new Recording(_published));

        await rig.RunAsync();

        rig.Outcome.UnderReview.Should().BeFalse();
    }

    [Fact]
    public void SpecDialogRevisionSection_InterruptedProposal_SaysItStandsInTheDraft()
    {
        var rig = new ProposalReviewRig();
        var proposal = rig.Propose(Epic()) with { UnderReview = true };
        rig.Pipeline.Set(ContextKeys.SpecDialogRevisedProposal, proposal);

        var section = SpecDialogRevisionSection.Render(rig.Pipeline);

        section.Should().Contain("The proposal your last turn wrote").And.Contain("It stands in your draft below");
    }

    [Fact]
    public void SpecDialogRevisionSection_OrdinaryEditWithoutFindings_RendersNothing()
    {
        var rig = new ProposalReviewRig();
        rig.Pipeline.Set(ContextKeys.SpecDialogRevisedProposal, rig.Propose(Epic()));

        SpecDialogRevisionSection.Render(rig.Pipeline).Should().BeEmpty();
    }
}
