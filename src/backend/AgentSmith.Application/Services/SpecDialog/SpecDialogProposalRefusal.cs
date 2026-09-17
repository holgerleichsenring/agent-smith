using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-09-17-042ec: refuses a proposal from a design turn that may not propose yet
/// (<see cref="SpecDialogProposalRule"/>), before the outcome is validated. A refusal is not a
/// validation failure: the turn is re-prompted once to answer instead, and a retry that still
/// proposes answers with its prose, the draft stripped — never with the "did not pass
/// validation" notice, which would be false and discard the discussion. A retry with no prose
/// left is a notice, and is recorded as one.
/// <para>
/// The retry continues the first pass's <see cref="MasterConversation"/>, like every other
/// re-prompt: the files it read and an ask_human answer it was given stay in the thread.
/// </para>
/// </summary>
public sealed class SpecDialogProposalRefusal(
    IAgenticLoopRunner loopRunner,
    ISpecDialogPromptFactory promptFactory,
    ILogger<SpecDialogProposalRefusal> logger)
{
    internal const string EmptyProseNotice =
        "I drafted a proposal before we had discussed this work, so I am not showing it. "
        + "Tell me what you want to change or clarify, and I will answer with what I found first.";

    public async Task<AgenticLoopResult> RefuseEarlyProposalAsync(
        PipelineContext pipeline, AgenticLoopRequest request, string userPrompt,
        AgenticLoopResult loopResult, MasterConversation conversation, PipelineCostTracker costTracker,
        CancellationToken ct)
    {
        if (!SpecDialogDraftBlocks.Contains(loopResult.Response.Text)
            || SpecDialogProposalRule.MayPropose(pipeline))
            return loopResult;

        logger.LogWarning("Design turn proposed before the operator replied to a discussion — re-prompting once");
        AgenticLoopResult retry;
        try
        {
            var nudge = promptFactory.BuildProposalRefusalNudge(userPrompt);
            retry = await loopRunner.RunAsync(request with { UserPrompt = nudge, PriorMessages = conversation.Thread() }, ct);
            conversation.Continued(nudge, retry.Response);
            costTracker.Track(retry.Response);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Proposal-refusal re-prompt failed — answering with the first reply's prose");
            return AnswerWithProse(pipeline, loopResult);
        }
        if (!SpecDialogDraftBlocks.Contains(retry.Response.Text)) return retry;
        logger.LogWarning("Design turn proposed again after the refusal — answering with its prose");
        return AnswerWithProse(pipeline, retry);
    }

    private static AgenticLoopResult AnswerWithProse(PipelineContext pipeline, AgenticLoopResult result)
    {
        var prose = SpecDialogDraftBlocks.Strip(result.Response.Text);
        if (prose.Length > 0) return MasterOutcomes.WithReplyText(result, prose);
        pipeline.Set(ContextKeys.SpecDialogReplyKind, SpecDialogTurnKind.Notice);
        return MasterOutcomes.WithReplyText(result, EmptyProseNotice);
    }
}
