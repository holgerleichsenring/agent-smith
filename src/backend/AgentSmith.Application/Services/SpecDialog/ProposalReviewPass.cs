using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Turns;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-11-12-cd4ch: the one advisory review pass over a design turn's proposal — the whole-set cut review and the
/// attack pass of a spec or an epic. It never changes a draft: the proposal comes back with what the pass found,
/// its rounds, the ids it could not review and the texts it called clean. The post-turn review and the
/// review_proposal tool run this same pass.
/// </summary>
public sealed class ProposalReviewPass(
    ProposalWholeSetReview wholeSet, ProposalAttackPass attack, ITurnActivityObserverAccessor turnActivity)
{
    public async Task<OutcomeProposal> ReviewAsync(
        ProposalTurn turn, SpecReviewSettings settings, OutcomeProposal proposal, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(turn);
        ArgumentNullException.ThrowIfNull(proposal);
        if (ProposalSpecSet.Drafts(proposal) is not { Count: > 0 } drafts) return proposal;
        await turnActivity.ReportAsync(new TurnActivity(TurnActivityKind.Reviewing), ct);
        var whole = await wholeSet.FindingsAsync(turn, drafts, ct) ?? [];
        var outcome = await attack.RunAsync(turn, settings, proposal, ct);
        return proposal with
        {
            Findings = [.. outcome.Standing, .. whole], ReviewRounds = outcome.Rounds, NotReviewed = outcome.NotReviewed,
            ReviewedClean = outcome.ReviewedClean,
        };
    }
}
