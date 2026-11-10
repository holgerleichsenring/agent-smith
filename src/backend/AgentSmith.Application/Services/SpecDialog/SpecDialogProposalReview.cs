using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Turns;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-09-17-042ed: reviews a design turn's spec or epic proposal INSIDE the turn — the read-only scopes are
/// disposed in the runner's finally — and hands what it found back on the proposal. A review that could not be
/// taken blocks nothing. 2026-10-02-3f06c: the evidence check runs first, each half in its own guard.
/// 2026-10-13-8762i: with spec_review.rounds above 0 the whole-set call runs once, then the rounds; 0 reviews once.
/// </summary>
public sealed class SpecDialogProposalReview(
    ISpecCutReviewer reviewer,
    DerivationLookFactory looks,
    ProposalEvidenceReview evidence,
    ISpecReviewSettingsReader settings, // 2026-10-13-8762i: read per turn, live
    ProposalReviewLoop rounds, TicketPlanReview plans, // 2026-10-14-dbcek: a list is reviewed as a plan
    ITurnActivityObserverAccessor turnActivity, // 2026-09-17-042ee: the review is not dark
    ILogger<SpecDialogProposalReview> logger, ITurnProposalObserverAccessor? proposals = null) // 2026-11-09-9a98
{
    /// <summary>The turn's reply: the one it had, unless rounds changed what it proposed.</summary>
    public async Task<AgenticLoopResult> ReviewAsync(
        PipelineContext pipeline, AgenticLoopRequest request, string userPrompt, AgenticLoopResult loopResult,
        MasterConversation conversation, AgentConfig agent, PipelineCostTracker costTracker, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!pipeline.TryGet<OutcomeProposal>(ContextKeys.SpecDialogOutcome, out var proposal) || proposal is null) return loopResult;
        // 2026-11-09-9a98: out before the review; an epic's parent is never reviewed, so only its children wait.
        var waiting = proposal is EpicOutcome epic ? epic.Children : ProposalSpecSet.Drafts(proposal);
        // 2026-11-10-18cf: each waiting draft says why, so a turn cancelled from here leaves a reason behind.
        await (proposals?.PublishUnderReviewAsync(
            ProposalReviewSummary.Held(proposal, waiting, proposal.Findings, ProposalReviewSummary.UnderReviewWhy), ct)
            ?? Task.CompletedTask);
        if (proposal is TicketGraphOutcome graph)
            return await plans.ReviewAsync(new ProposalTurn(pipeline, request, userPrompt, conversation, agent, costTracker), graph, loopResult, ct);
        if (ProposalSpecSet.Drafts(proposal) is not { Count: > 0 } drafts) return loopResult;

        await turnActivity.ReportAsync(new TurnActivity(TurnActivityKind.Reviewing), ct);
        if (new ProposalReviewSettingsReader(settings, logger).Read() is not { Review.Rounds: > 0 } current)
        {
            await SingleAsync(pipeline, proposal, drafts, agent, costTracker, ct);
            return loopResult;
        }
        var wholeSet = await ModelFindingsAsync(pipeline, drafts, agent, costTracker, ct) ?? [];
        var turn = new ProposalTurn(pipeline, request, userPrompt, conversation, agent, costTracker);
        var outcome = await rounds.RunAsync(turn, current, proposal, loopResult, wholeSet, ct);
        logger.LogInformation("The design turn's proposal was reviewed in {Rounds} round(s); {Waiting} spec(s) not reviewed yet",
            outcome.Rounds.Count, outcome.NotReviewed?.Count ?? 0);
        pipeline.Set(ContextKeys.SpecDialogOutcome, outcome.Proposal with
        {
            Findings = [.. outcome.Standing, .. wholeSet], ReviewRounds = outcome.Rounds, NotReviewed = outcome.NotReviewed,
        });
        return outcome.Reply;
    }

    private async Task SingleAsync(
        PipelineContext pipeline, OutcomeProposal proposal, IReadOnlyList<SpecDraft> drafts, AgentConfig agent,
        PipelineCostTracker costTracker, CancellationToken ct)
    {
        var findings = new List<ProposalFinding>();
        try
        {
            findings.AddRange(await evidence.FindingsAsync(pipeline, proposal, ct));
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "The evidence of this design turn's proposal could not be checked");
        }
        // 2026-11-10-18cf: with review rounds off this one call is the review; when it fails, nothing was checked.
        if (await ModelFindingsAsync(pipeline, drafts, agent, costTracker, ct) is not { } model)
        {
            pipeline.Set(ContextKeys.SpecDialogOutcome, ProposalReviewSummary.Held(proposal, drafts, findings, ReviewCallFailedWhy));
            return;
        }
        findings.AddRange(model);
        if (findings.Count == 0) return;
        logger.LogInformation(
            "The review of this design turn's proposal reported {Count} finding(s)", findings.Count);
        pipeline.Set(ContextKeys.SpecDialogOutcome, proposal with { Findings = findings });
    }

    public const string ReviewCallFailedWhy = "the review call failed";

    /// <summary>The whole-set review's findings, or null when its call failed.</summary>
    private async Task<IReadOnlyList<ProposalFinding>?> ModelFindingsAsync(
        PipelineContext pipeline, IReadOnlyList<SpecDraft> drafts, AgentConfig agent,
        PipelineCostTracker costTracker, CancellationToken ct)
    {
        // The look owns none of the turn's repositories — the runner disposes them — so nothing is disposed.
        var look = looks.ForProposalReview(pipeline);
        var key = pipeline.TryGet<string>(ContextKeys.DialogueJobId, out var jobId) && jobId is not null
            ? jobId
            : "spec-dialog";
        try
        {
            var review = await reviewer.ReviewAsync(drafts, key, ticketText: null, look, agent, costTracker, ct);
            return SpecDialogProposalFindings.Of(review, look?.Evidence.Looks);
        }
        // On the RUN's token, not on the exception's type: an LLM NetworkTimeout arrives as a
        // TaskCanceledException on an uncancelled token, and letting it escape here would destroy
        // the reply this turn already has — the call sits outside the master handler's own guard.
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "The review of this design turn's proposal could not be taken");
            return null;
        }
    }
}
