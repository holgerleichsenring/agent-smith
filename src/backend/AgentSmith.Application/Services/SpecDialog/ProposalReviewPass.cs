using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Turns;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-11-12-cd4ch: the one advisory review pass over a design turn's proposal — a ticket list's plan review
/// (2026-11-12-cd4ci), or the whole-set cut review and the attack pass of a spec or an epic. It never changes what was
/// proposed: the proposal comes back with what the pass found, its rounds, the ids it could not review and the texts
/// it called clean. The post-turn review and the review_proposal tool run this same pass.
/// </summary>
public sealed class ProposalReviewPass(
    ProposalWholeSetReview wholeSet, ProposalAttackPass attack, TicketPlanReview plans, ITurnActivityObserverAccessor turnActivity)
{
    public async Task<OutcomeProposal> ReviewAsync(
        ProposalTurn turn, SpecReviewSettings settings, OutcomeProposal proposal, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(turn);
        ArgumentNullException.ThrowIfNull(proposal);
        if (!Reviewable(proposal)) return proposal;
        await turnActivity.ReportAsync(new TurnActivity(TurnActivityKind.Reviewing), ct);
        if (proposal is TicketGraphOutcome graph) return await plans.ReviewAsync(turn, graph, ct);
        var whole = await wholeSet.FindingsAsync(turn, ProposalSpecSet.Drafts(proposal), ct) ?? [];
        var outcome = await attack.RunAsync(turn, settings, proposal, ct);
        return proposal with
        {
            Findings = [.. outcome.Standing, .. whole], ReviewRounds = outcome.Rounds, NotReviewed = outcome.NotReviewed,
            ReviewedClean = outcome.ReviewedClean,
        };
    }

    /// <summary>A ticket list, a spec or an epic; an answer, a bug or a comment has nothing a review pass reads.</summary>
    public static bool Reviewable(OutcomeProposal proposal) =>
        proposal is TicketGraphOutcome || ProposalSpecSet.Drafts(proposal).Count > 0;
}
