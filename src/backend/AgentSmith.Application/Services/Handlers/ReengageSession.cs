using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-10-13-9a59f: what the open loop's re-engagement works with — the master's request and prompt,
/// its tool hosts, the cost tracker and the conversation every drive continues. Built once per master
/// step by <see cref="AgenticMasterHandler"/>.
/// </summary>
internal sealed record ReengageSession(
    AgenticMasterContext Context,
    AgenticLoopRequest Request,
    string UserPrompt,
    string? PipelineName,
    ProgressLedgerToolHost Progress,
    FilesystemToolHost Fs,
    LogDecisionToolHost Log,
    VerdictOwed VerdictOwed,
    PipelineCostTracker CostTracker,
    Action<ChatResponse> TrackMasterResponse,
    TicketClarificationToolHost? TicketClarifications,
    MasterConversation Conversation,
    Action<int> SetPass,
    Contracts.Decisions.IDecisionLogger? Decisions = null);

/// <summary>2026-10-13-9a59f: the loop's result so far — the last drive, the run's changes, the standing verdict.</summary>
internal sealed record ReengageState(
    AgenticLoopResult LoopResult, IReadOnlyList<CodeChange> Changes, MasterVerification? Verification);

/// <summary>
/// 2026-10-13-9a59f: one re-engagement pass's outcome — the state after it, the verdict it emitted itself
/// (null when none), and the reason it ended the loop (null when the loop decides again). The progress
/// outcome and the blocker it claimed are what the surrender challenge reads a stop by.
/// </summary>
internal sealed record ReengagePassResult(
    ReengageState State, MasterVerification? Emitted, string? Stop,
    ReengageOutcome Outcome = ReengageOutcome.Continue, MasterBlockedClaim? Block = null);
