using System;
using AgentSmith.Contracts.Models.Configuration;
using Microsoft.Extensions.AI;

namespace AgentSmith.Contracts.Providers;

/// <summary>
/// p0341c: the per-pass hooks the master's chat-client middleware seam calls INSIDE the
/// FunctionInvokingChatClient loop — the mechanism that turns the master loop from a
/// capped single-shot into an open, budget-bounded, self-reminding pass. All fields are
/// plain delegates so the Contracts layer stays free of the Application-layer machinery
/// (PipelineCostTracker, ProgressLedger renderer) they close over.
///
/// <para>Passed on <c>AgenticLoopRequest</c> and threaded through
/// <c>IChatClientFactory.Create</c> into a <c>DelegatingChatClient</c> below
/// UseFunctionInvocation, so each tool iteration re-enters these hooks:
/// <list type="bullet">
///   <item><see cref="IsBudgetExhausted"/> — the WITHIN-pass money fence: checked before
///     each iteration; true throws <see cref="MasterBudgetExhaustedException"/> to stop a
///     runaway single pass (the 200-iteration ceiling is only the anti-runaway net).</item>
///   <item><see cref="RecordIterationUsage"/> — feeds each iteration's usage into the
///     pass-local budget estimator so the fence tracks the live spend.</item>
///   <item><see cref="RenderReminder"/> — the ledger discipline reminder, injected as a
///     synthetic user message after <see cref="ReminderEveryNIterations"/> iterations
///     WITHOUT an update_progress call (p0359: staleness — a model that keeps its ledger
///     current is never interrupted) and on drift.</item>
/// </list></para>
/// </summary>
public sealed record MasterLoopHooks(
    Func<bool>? IsBudgetExhausted = null,
    Action<ChatResponse>? RecordIterationUsage = null,
    Func<string?>? RenderReminder = null,
    int ReminderEveryNIterations = 10,
    int DriftEditlessIterations = 8,
    // p0341d: the compaction PIN carriers + config. When Compaction is enabled, a
    // CompactingChatClient (below the governor, below UseFunctionInvocation) reduces the
    // message list in-flight once it crosses the threshold: it PINS the system prompt +
    // RenderLedgerForPin() (the checklist) + RenderWorkingStateForPin() (decisions + last
    // build/test — the continuity) + the recent tail verbatim, and summarizes the evicted
    // middle incrementally. Rendered from PipelineContext at compaction time (CURRENT
    // state), never a pass-start snapshot. Null accessors => that pin part is omitted.
    Func<string?>? RenderLedgerForPin = null,
    Func<string?>? RenderWorkingStateForPin = null,
    CompactionConfig? Compaction = null);

/// <summary>
/// p0341c: raised by the within-pass budget middleware when the running cost crosses the
/// per-pipeline cap mid-pass. Distinct from a generic loop failure so the master handler
/// stops CLEANLY — publishes the partial CodeChanges + the current ledger and finalizes
/// the run as an honest cost-cap-exhausted outcome, never a laundered green.
/// </summary>
public sealed class MasterBudgetExhaustedException : Exception
{
    public MasterBudgetExhaustedException(string message) : base(message) { }
}
