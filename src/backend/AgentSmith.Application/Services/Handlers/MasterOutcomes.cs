using AgentSmith.Application.Extensions;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Progress;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Builds the handler's loop results — published, failed, reply-carrying — and the
/// operator-facing description of a master failure. p0403 lifted them out of the
/// handler.
/// </summary>
internal static class MasterOutcomes
{
    internal static AgenticLoopResult PublishOutcome(
        PipelineContext pipeline, OutcomeProposal proposal, AgenticLoopResult result)
    {
        pipeline.Set(ContextKeys.SpecDialogOutcome, proposal);
        return result;
    }

    // A twice-invalid outcome degrades to an honest answer: the notice is the
    // reply and nothing is proposed for routing.
    internal static AgenticLoopResult FailOutcome(
        PipelineContext pipeline, AgenticLoopResult result, string error)
    {
        pipeline.Set(ContextKeys.SpecDialogOutcome, (OutcomeProposal)new AnswerOutcome());
        return WithReplyText(result, OutcomeFailureNotice(error));
    }

    internal static AgenticLoopResult WithReplyText(AgenticLoopResult result, string text) =>
        result with { Response = new ChatResponse(new ChatMessage(ChatRole.Assistant, text)) };

    internal static string OutcomeFailureNotice(string error) =>
        "I proposed an outcome for this design turn, but it did not pass validation "
        + $"({error}), so I am not showing it. Refine the requirements or ask me to "
        + "draft again.";

    // p0237: turn the master loop's exception into an operator-actionable reason.
    // An OperationCanceledException here (the run token was NOT cancelled — see
    // the caller's `when` guard) is an internal LLM-layer timeout, not a real
    // cancel; name the lever. Everything else carries its type + message.
    internal static string DescribeMasterFailure(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
        {
            if (e is OperationCanceledException)
                return "The coding agent was cut off by an internal timeout (not an "
                    + "operator cancel). If a build/test command was running it likely "
                    + "exceeded sandbox.run_command_timeout_seconds; if an LLM call "
                    + "stalled, raise the agent's network_timeout_seconds (default 300s). "
                    + "Partial work, if any, was preserved.";
        }
        return $"The coding agent failed: {ex.GetType().Name}: {ex.Message}";
    }
}
