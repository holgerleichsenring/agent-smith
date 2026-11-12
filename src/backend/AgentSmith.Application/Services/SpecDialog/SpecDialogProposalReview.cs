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
/// on, ONE advisory pass (<see cref="ProposalReviewPass"/>); off, the mechanical checks only
/// (<see cref="ProposalMechanicalChecks"/>). Nothing re-prompts the design partner, and the reply is the turn's own.
/// </summary>
public sealed class SpecDialogProposalReview(
    ProposalMechanicalChecks checks, ISpecReviewSettingsReader settings, ProposalReviewPass pass,
    ILogger<SpecDialogProposalReview> logger, ITurnProposalObserverAccessor? proposals = null)
{
    /// <summary>Sets the reviewed proposal on the pipeline; a turn without one is left as it is.</summary>
    public async Task ReviewAsync(PipelineContext pipeline, AgentConfig agent, PipelineCostTracker costTracker, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!pipeline.TryGet<OutcomeProposal>(ContextKeys.SpecDialogOutcome, out var proposal) || proposal is null
            || !ProposalReviewPass.Reviewable(proposal)) return;
        // 2026-11-10-8e59: a block that cannot be used reviews nothing here; the review before building attacks it.
        var read = new ProposalReviewSettingsReader(settings, logger).Read();
        OutcomeProposal reviewed;
        if (read.Problem is { } problem) reviewed = Unread(proposal, problem);
        else if (read.Settings is not { Review.DesignTurn: true } on) reviewed = await checks.CheckAsync(pipeline, proposal, ct);
        else
        {
            await PublishUnderReviewAsync(pipeline, proposal, ct);
            reviewed = await pass.ReviewAsync(new ProposalTurn(pipeline, agent, costTracker), on, proposal, ct);
        }
        pipeline.Set(ContextKeys.SpecDialogOutcome, reviewed);
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
