using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Turns;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-09-17-042ed: reviews a design turn's spec or epic proposal after the turn — the read-only scopes are
/// disposed in the runner's finally — and hands what it found back on the proposal. 2026-11-12-cd4ch: with
/// spec_review.design_turn on, ONE advisory pass (<see cref="ProposalReviewPass"/>); off, the mechanical evidence
/// check only. Nothing re-prompts the design partner, and the reply is the turn's own.
/// </summary>
public sealed class SpecDialogProposalReview(
    ProposalEvidenceReview evidence, ISpecReviewSettingsReader settings, ProposalReviewPass pass,
    TicketPlanReview plans, ILogger<SpecDialogProposalReview> logger, ITurnProposalObserverAccessor? proposals = null)
{
    /// <summary>The turn's reply, unchanged; the reviewed proposal is set on the pipeline.</summary>
    public async Task<AgenticLoopResult> ReviewAsync(
        PipelineContext pipeline, AgenticLoopRequest request, string userPrompt, AgenticLoopResult loopResult,
        MasterConversation conversation, AgentConfig agent, PipelineCostTracker costTracker, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!pipeline.TryGet<OutcomeProposal>(ContextKeys.SpecDialogOutcome, out var proposal) || proposal is null) return loopResult;
        var turn = new ProposalTurn(pipeline, request, userPrompt, conversation, agent, costTracker);
        if (proposal is TicketGraphOutcome graph)
        {
            await PublishUnderReviewAsync(pipeline, proposal, ProposalSpecSet.Drafts(proposal), ct);
            return await plans.ReviewAsync(turn, graph, loopResult, ct);
        }
        if (ProposalSpecSet.Drafts(proposal).Count == 0) return loopResult;
        // An epic's parent is never reviewed, so only its children wait.
        var waiting = proposal is EpicOutcome epic ? epic.Children : ProposalSpecSet.Drafts(proposal);
        // 2026-11-10-8e59: a block that cannot be used reviews nothing here; the review before building attacks it.
        var read = new ProposalReviewSettingsReader(settings, logger).Read();
        if (read.Problem is { } problem)
            pipeline.Set(ContextKeys.SpecDialogOutcome, ProposalReviewSummary.MarkedNotReviewed(proposal, waiting, proposal.Findings, problem));
        else if (read.Settings is not { Review.DesignTurn: true })
            pipeline.Set(ContextKeys.SpecDialogOutcome, proposal with { Findings = await EvidenceAsync(pipeline, proposal, ct) });
        else
        {
            await PublishUnderReviewAsync(pipeline, proposal, waiting, ct);
            pipeline.Set(ContextKeys.SpecDialogOutcome, await pass.ReviewAsync(turn, read.Settings, proposal, ct));
        }
        return loopResult;
    }

    // 2026-11-09-9a98: out before the review; each waiting draft says why, so a turn cancelled from here leaves a reason.
    private async Task PublishUnderReviewAsync(
        PipelineContext pipeline, OutcomeProposal proposal, IReadOnlyList<SpecDraft> waiting, CancellationToken ct) =>
        await (proposals?.PublishUnderReviewAsync(ProposalReviewSummary.MarkedNotReviewed(
            proposal, waiting, proposal.Findings, ProposalReviewSummary.UnderReviewWhy) with { Spend = TurnSpend.Of(pipeline) }, ct)
            ?? Task.CompletedTask);

    /// <summary>The cited files the drafts name that do not resolve — read, no model call.</summary>
    private async Task<IReadOnlyList<ProposalFinding>> EvidenceAsync(PipelineContext pipeline, OutcomeProposal proposal, CancellationToken ct)
    {
        try
        {
            return await evidence.FindingsAsync(pipeline, proposal, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "The evidence of this design turn's proposal could not be checked");
            return [];
        }
    }
}
