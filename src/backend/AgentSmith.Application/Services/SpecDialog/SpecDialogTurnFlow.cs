using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.Prompts;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// What a dialog turn is asked and what happens to its reply, by the master that leads it. The design partner gets
/// the design contract, and the proposal its write tools stored is published and reviewed. The ticket steward gets its
/// own contract and its reply is an answer: no proposal rule, refusal, draft parse or review runs, so a quoted
/// ```yaml block is never read as a draft.
/// </summary>
public sealed class SpecDialogTurnFlow(
    ISpecDialogPromptFactory designPrompt,
    ITicketStewardPromptFactory stewardPrompt,
    SpecDialogProposalReview proposalReview) // 2026-09-17-042ed: the turn reviews what it proposes
{
    public string Prompt(AgenticMasterContext context, DialogImageParts images)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(images);
        return context.IsTicketSteward
            ? stewardPrompt.Build(context.Pipeline, images.Existing, images.Carried)
            : designPrompt.Build(context.Pipeline, images.Existing, images.Carried);
    }

    /// <summary>The turn's reply, with its outcome published for CollectSpecDialogReply.</summary>
    public async Task<AgenticLoopResult> ConcludeAsync(
        AgenticMasterContext context, AgenticLoopRequest request, string userPrompt, AgenticLoopResult loopResult,
        MasterConversation conversation, PipelineCostTracker costTracker, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.IsTicketSteward) return MasterOutcomes.PublishOutcome(context.Pipeline, new AnswerOutcome(), loopResult);
        // 2026-11-12-cd4ca/cd4cb: the proposal is what the write tools stored, each write already checked; nothing
        // after the loop re-prompts the turn, and a reply cut at the output limit is prose published as it came.
        var proposal = context.Pipeline.TryGet<SpecDialogReplySlot>(ContextKeys.SpecDialogReplySlot, out var slot) && slot is not null
            ? Writes.ProposalDraftOutcome.For(slot.Proposal) : new AnswerOutcome();
        var gated = MasterOutcomes.PublishOutcome(context.Pipeline, proposal, loopResult);
        await proposalReview.ReviewAsync(context.Pipeline, context.AgentConfig, costTracker, ct); // cd4ch: advisory, the reply stands
        return gated;
    }
}
