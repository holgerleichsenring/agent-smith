using System.Linq;
using AgentSmith.Contracts.Progress;
using AgentSmith.Domain.Entities;
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
    /// <summary>p0391: the pass refilled its own checklist. Every step left actionable at the
    /// end of the pass is one the model invented DURING it, and the pass added nothing to the
    /// diff — the ledger turned over without the work moving.</summary>
    StopSelfRefilled,
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
    /// <summary>Decide the driver action after a pass from its tool-call count, any blocked
    /// claim, and whether the pass refilled its own checklist (p0391).</summary>
    public static ReengageOutcome Decide(
        int toolCallsInPass, MasterBlockedClaim? block, bool passEndedOnException,
        bool ledgerSelfRefilled = false)
    {
        if (passEndedOnException) return ReengageOutcome.Continue;   // a crash is recovery, not idleness
        if (ShouldRespectBlock(block)) return ReengageOutcome.StopBlocked;
        if (toolCallsInPass <= 0) return ReengageOutcome.StopIdle;   // empty pass — the only stall signal
        if (ledgerSelfRefilled) return ReengageOutcome.StopSelfRefilled;
        return ReengageOutcome.Continue;
    }

    /// <summary>
    /// p0391: the ledger-turnover bound. Since p0374 the ledger is fully model-owned with no
    /// turnover limit, and ShouldReengage re-drives on any actionable item — so a model that
    /// closes its checklist and appends "verify X once more" re-drives itself indefinitely
    /// (observed: a 124-minute run cancelled by the operator with the work long finished).
    /// A pass is SELF-REFILLED when NO actionable step left at its end survived from the
    /// checklist it started with AND it added nothing to the diff: the model both invented all
    /// the remaining work and produced none. Both halves are mechanical — ids come from the
    /// ledger the model itself replaced, the diff from the write log the tool host keeps.
    /// A productive pass never trips it: any edit sets <paramref name="producedNewWork"/>, and
    /// any surviving pending step (the ordinary "work the checklist" shape) keeps it false.
    /// </summary>
    public static bool IsSelfRefilled(
        IReadOnlyCollection<string> ledgerItemIdsAtPassStart, ProgressLedger ledgerAfterPass,
        bool producedNewWork)
    {
        if (producedNewWork) return false;
        var pending = ledgerAfterPass.ActionablePending;
        // A drained ledger is the ordinary completion path — ShouldReengage owns it.
        if (pending.Count == 0) return false;
        return pending.All(e => !ledgerItemIdsAtPassStart.Contains(e.Id));
    }

    /// <summary>
    /// p0391: did the pass ADD to the run's diff? The write log is the filesystem tool host's
    /// own record of what actually landed in the working tree (new file, or new content on a
    /// file already touched), so the model cannot claim work it did not do. Entry order is
    /// stable — writes append, a re-write replaces in place.
    /// </summary>
    public static bool ProducedNewWork(
        IReadOnlyList<CodeChange> before, IReadOnlyList<CodeChange> after)
    {
        if (after.Count != before.Count) return true;
        for (var i = 0; i < after.Count; i++)
            if (!string.Equals(after[i].Path.Value, before[i].Path.Value, StringComparison.Ordinal)
                || !string.Equals(after[i].Content, before[i].Content, StringComparison.Ordinal))
                return true;
        return false;
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
