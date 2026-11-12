using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Turns;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Turns;
using FluentAssertions;
using static AgentSmith.Tests.SpecDialog.ProposalReview.ProposalReviewRig;

namespace AgentSmith.Tests.SpecDialog.ProposalReview;

/// <summary>2026-11-10-d854a: every push made while a turn's review runs carries what the turn has spent so far.</summary>
public sealed class ProposalSpendTests
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
    public async Task SpecDialogProposalReview_FirstUnderReviewPush_CarriesSpend()
    {
        var rig = Spending(3m);
        using var observing = _accessor.Observe(new Recording(_published));

        await rig.RunAsync();

        _published[0].Spend.Should().NotBeNull();
        _published[0].Spend!.Usd.Should().Be(3m);
    }

    private ProposalReviewRig Spending(decimal usd)
    {
        var rig = new ProposalReviewRig { Proposals = _accessor };
        rig.Propose(Epic());
        PipelineCostTracker.GetOrCreate(rig.Pipeline).SeedPriorSpend(OpenQuestionsTests.Spent(usd));
        return rig;
    }
}
