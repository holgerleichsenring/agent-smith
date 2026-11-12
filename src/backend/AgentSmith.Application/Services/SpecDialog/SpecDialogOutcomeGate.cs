using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.SpecDialog.Writes;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// p0315b/p0315e: publishes a design turn's typed terminal outcome for CollectSpecDialogReply. 2026-11-12-cd4ca: the
/// outcome is what the turn's write tools stored, never a block parsed from the reply. A reply cut at the output
/// limit is re-asked once under the raised budget; a second cut fails the turn's reply with a notice.
/// </summary>
public sealed class SpecDialogOutcomeGate(
    IAgenticLoopRunner loopRunner,
    ILogger<SpecDialogOutcomeGate> logger)
{
    private const string Cut = "the reply was cut off at the output limit before it ended; write the whole reply again";

    public async Task<AgenticLoopResult> GateAsync(
        PipelineContext pipeline, AgenticLoopRequest request, string userPrompt,
        AgenticLoopResult loopResult, MasterConversation conversation, PipelineCostTracker costTracker, CancellationToken ct)
    {
        if (loopResult.Response.FinishReason != ChatFinishReason.Length) return Published(pipeline, loopResult);
        AgenticLoopResult retry;
        try
        {
            var nudge = $"{Cut}.\n\nOriginal task:\n{userPrompt}";
            retry = await loopRunner.RunAsync(request with
            {
                UserPrompt = nudge, PriorMessages = conversation.Thread(),
                MaxOutputTokensOverride = request.AgentConfig.DialogMasterMaxOutputTokens,
            }, ct);
            conversation.Continued(nudge, retry.Response);
            costTracker.Track(retry.Response);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "The re-ask of a design reply cut at the output limit failed");
            return MasterOutcomes.FailOutcome(pipeline, loopResult, Cut);
        }
        return retry.Response.FinishReason == ChatFinishReason.Length
            ? MasterOutcomes.FailOutcome(pipeline, retry, Cut)
            : Published(pipeline, retry);
    }

    private static AgenticLoopResult Published(PipelineContext pipeline, AgenticLoopResult result) =>
        MasterOutcomes.PublishOutcome(pipeline,
            pipeline.TryGet<SpecDialogReplySlot>(ContextKeys.SpecDialogReplySlot, out var slot) && slot is not null
                ? ProposalDraftOutcome.For(slot.Proposal) : new AnswerOutcome(), result);
}
