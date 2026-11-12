using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Turns;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-09-17-042ed: reviews a design turn's proposal after the turn — the read-only scopes are disposed in the
/// runner's finally — and hands what it found back on the proposal. 2026-11-12-cd4ch: with spec_review.design_turn
/// on, ONE pass (<see cref="ProposalReviewPass"/>); off, the mechanical checks only
/// (<see cref="ProposalMechanicalChecks"/>). 2026-11-12-92e9b: with the pass on, a draft whose every text this turn
/// already reviewed takes the ledger's results and nothing runs; a pass that ran hands its report back to the master.
/// </summary>
public sealed class SpecDialogProposalReview(
    ProposalMechanicalChecks checks, ISpecReviewSettingsReader settings, ProposalReviewPass pass,
    ILogger<SpecDialogProposalReview> logger, ITurnProposalObserverAccessor? proposals = null)
{
    /// <summary>The reviewed proposal and the report for the master; a proposal with nothing to review comes back as it is.</summary>
    public async Task<DesignTurnReview> ReviewAsync(ProposalTurn turn, OutcomeProposal proposal, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(turn);
        ArgumentNullException.ThrowIfNull(proposal);
        if (!ProposalReviewPass.Reviewable(proposal)) return new(proposal, null);
        // 2026-11-10-8e59: a block that cannot be used reviews nothing here; the review before building attacks it.
        var read = new ProposalReviewSettingsReader(settings, logger).Read();
        if (read.Problem is { } problem) return new(Unread(proposal, problem), null);
        if (read.Settings is not { Review.DesignTurn: true } on)
            return new(await checks.CheckAsync(turn.Pipeline, proposal, ct), null);
        var ledger = ProposalReviewLedger.Of(turn.Pipeline);
        if (ledger.Covers(proposal)) return new(ledger.Compose(proposal), null);
        await PublishUnderReviewAsync(turn.Pipeline, proposal, ct);
        var reviewed = await pass.ReviewAsync(turn, on, proposal, ct);
        return new(reviewed, DesignTurnReviewReport.For(reviewed));
    }

    private static OutcomeProposal Unread(OutcomeProposal proposal, string problem) => proposal is TicketGraphOutcome graph
        ? graph with { Findings = [new ProposalFinding(TicketPlanReviewPrompt.ListKey, ProposalReviewSummary.NotReviewedYet, problem)] }
        : ProposalReviewSummary.MarkedNotReviewed(proposal, Waiting(proposal), proposal.Findings, problem);

    // 2026-11-09-9a98: out before the review; each waiting draft says why, so a turn cancelled from here leaves a reason.
    private async Task PublishUnderReviewAsync(PipelineContext pipeline, OutcomeProposal proposal, CancellationToken ct) =>
        await (proposals?.PublishUnderReviewAsync(ProposalReviewSummary.MarkedNotReviewed(
            proposal, Waiting(proposal), proposal.Findings, ProposalReviewSummary.UnderReviewWhy) with { Spend = TurnSpend.Of(pipeline) }, ct)
            ?? Task.CompletedTask);

    /// <summary>The drafts a review attacks: an epic's children (its parent is never built), else every draft.</summary>
    private static IReadOnlyList<SpecDraft> Waiting(OutcomeProposal proposal) =>
        proposal is EpicOutcome epic ? epic.Children : ProposalSpecSet.Drafts(proposal);
}
