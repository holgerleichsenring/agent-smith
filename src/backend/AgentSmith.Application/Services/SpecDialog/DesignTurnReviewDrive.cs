using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-11-12-92e9b: the design partner reads the review of its final draft before it answers. When its loop ends
/// with a draft text no pass of this turn reviewed, the pass runs; what it found goes back to the master on its own
/// thread as a review report, and the master answers again. It ends when the loop ends with every draft text reviewed
/// — a property of the text, never a count. A pass that reviewed nothing sends nothing back. A report pass that throws
/// keeps the previous reply and draft. Reverses 2026-11-12-cd4cb's no-re-prompt on purpose.
/// </summary>
public sealed class DesignTurnReviewDrive(
    SpecDialogProposalReview review, IAgenticLoopRunner loopRunner, ILogger<DesignTurnReviewDrive> logger)
{
    /// <summary>The turn's last reply and its reviewed proposal, which the caller publishes.</summary>
    public async Task<(AgenticLoopResult Result, OutcomeProposal Proposal)> DriveAsync(
        AgenticMasterContext context, AgenticLoopRequest request, AgenticLoopResult loopResult,
        MasterConversation conversation, PipelineCostTracker costTracker, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(conversation);
        var slot = context.Pipeline.TryGet<SpecDialogReplySlot>(ContextKeys.SpecDialogReplySlot, out var s) ? s : null;
        var turn = new ProposalTurn(context.Pipeline, context.AgentConfig, costTracker);
        while (true)
        {
            var step = await review.ReviewAsync(turn, Proposal(slot), ct);
            if (step.Report is not { } report || slot is null) return (loopResult, step.Reviewed);
            if (await ReportAsync(request, report, conversation, costTracker, slot, ct) is not { } next)
                return (loopResult, step.Reviewed);
            loopResult = next;
        }
    }

    private async Task<AgenticLoopResult?> ReportAsync(
        AgenticLoopRequest request, string report, MasterConversation conversation, PipelineCostTracker costTracker,
        SpecDialogReplySlot slot, CancellationToken ct)
    {
        var (draft, changed, questions) = (slot.Proposal.Current, slot.Proposal.Changed, slot.Questions);
        try
        {
            var next = await loopRunner.RunAsync(
                request with { UserPrompt = report, PriorMessages = conversation.Thread(), UserImageParts = null }, ct);
            conversation.Continued(report, next.Response);
            costTracker.Track(next.Response);
            return next;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "The design partner's pass over its review report failed — keeping the reply before it");
            slot.Proposal.Seed(draft, changed);
            slot.Questions = questions;
            return null;
        }
    }

    private static OutcomeProposal Proposal(SpecDialogReplySlot? slot) =>
        slot is null ? new AnswerOutcome() : Writes.ProposalDraftOutcome.For(slot.Proposal);
}
