using System.Linq;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>What the open-loop driver should do after a pass.</summary>
public enum ReengageOutcome
{
    Continue,
    /// <summary>The pass called NO tool — the model is idle / has nothing left to do. The one
    /// reliable, unfakeable stall signal: agent-smith infrastructure was not invoked at all.</summary>
    StopIdle,
    /// <summary>An honest blocked claim backed by a concrete blocker — respected.</summary>
    StopBlocked,
}

/// <summary>
/// p0365: the open loop's stop policy — as little control as possible, as much as necessary.
///
/// A "pass" is one fresh nudged mini-conversation (AgenticLoopRunner.RunAsync): the model runs
/// the tool loop until it emits a tool-less response. It is NOT a unit of progress — the model
/// naturally alternates edit passes and verify passes (a `dotnet test` pass writes nothing yet
/// is the essential next step), so classifying each pass by state deltas mis-fires (it killed a
/// productive run 4c32 right as it moved from build-fix to tests).
///
/// The only signal we can read RELIABLY is: did any tool fire this pass? Zero tool calls means
/// the model, when re-engaged, did nothing — idle or giving up. That is the stop. Everything
/// else ("is it converging?") is genuinely hard to judge, so we do NOT gate on it — we keep
/// driving while any tool fires, bounded by budget / wall-time / the hard cap (the caller), and
/// surface the ambiguous signals (repeated reads/edits) to the operator rather than auto-aborting.
/// A "can't" claim is honoured only with a concrete blocker — symmetry with the diff-verified
/// "done" gate. Pure + testable.
/// </summary>
public static class ReengageProgressPolicy
{
    /// <summary>Decide the driver action after a pass from its tool-call count and any blocked claim.</summary>
    public static ReengageOutcome Decide(int toolCallsInPass, MasterBlockedClaim? block, bool passEndedOnException)
    {
        if (passEndedOnException) return ReengageOutcome.Continue;   // a crash is recovery, not idleness
        if (ShouldRespectBlock(block)) return ReengageOutcome.StopBlocked;
        if (toolCallsInPass <= 0) return ReengageOutcome.StopIdle;   // empty pass — the only stall signal
        return ReengageOutcome.Continue;
    }

    /// <summary>A blocked claim is respected only when a concrete blocker backs it (else re-driven).</summary>
    public static bool ShouldRespectBlock(MasterBlockedClaim? claim) =>
        claim is { IsBlocked: true } && !string.IsNullOrWhiteSpace(claim.Blocker);

    /// <summary>
    /// Tool calls the model made during a pass — the FunctionCallContent across the returned
    /// conversation (mirrors SubAgentRunner's count). Zero = an empty pass. M.E.AI's
    /// FunctionInvokingChatClient includes every iteration's tool-call message in Response.Messages.
    /// </summary>
    public static int CountToolCalls(ChatResponse? response) =>
        response?.Messages?.Sum(m => m.Contents?.OfType<FunctionCallContent>().Count() ?? 0) ?? 0;
}
