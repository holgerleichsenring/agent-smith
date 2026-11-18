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
/// the design contract, and the proposal its write tools stored is published with the mechanical checks' findings.
/// The ticket steward gets its own contract and its reply is an answer: no proposal rule, refusal, draft parse or
/// check runs, so a quoted ```yaml block is never read as a draft. 2026-11-18-9c1ca: the turn ends when the master
/// ends — no review runs after it and nothing sends it round again; a review the master wants, it commissions.
/// </summary>
public sealed class SpecDialogTurnFlow(
    ISpecDialogPromptFactory designPrompt,
    ITicketStewardPromptFactory stewardPrompt,
    ProposalMechanicalChecks checks)
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
        AgenticMasterContext context, AgenticLoopResult loopResult, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.IsTicketSteward) return MasterOutcomes.PublishOutcome(context.Pipeline, new AnswerOutcome(), loopResult);
        // 2026-11-12-cd4ca: the proposal is what the write tools stored, each write already checked.
        var proposal = context.Pipeline.TryGet<SpecDialogReplySlot>(ContextKeys.SpecDialogReplySlot, out var slot) && slot is not null
            ? await checks.CheckAsync(context.Pipeline, Writes.ProposalDraftOutcome.For(slot.Proposal), ct)
            : new AnswerOutcome();
        return MasterOutcomes.PublishOutcome(context.Pipeline, proposal, loopResult);
    }
}
