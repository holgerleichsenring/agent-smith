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
    PipelineCostTracker CostTracker,
    Action<ChatResponse> TrackMasterResponse,
    TicketClarificationToolHost? TicketClarifications,
    MasterConversation Conversation,
    Action<int> SetPass,
    Contracts.Decisions.IDecisionLogger? Decisions = null,
    // 2026-11-18-c8e2a: the step's sub-agents, stopped after each drive before its changes are read.
    SubAgentTeam? Team = null);

/// <summary>2026-10-13-9a59f: the loop's result so far — the last drive, the run's changes, and (2026-11-22-96c8a) the
/// texts the step's passes ended on, which make its report.</summary>
internal sealed record ReengageState(
    AgenticLoopResult LoopResult, IReadOnlyList<CodeChange> Changes, IReadOnlyList<string> Reports);

/// <summary>
/// 2026-10-13-9a59f: one re-engagement pass's outcome — the state after it, whether the pass ended on a report of
/// its own (2026-11-22-96c8a), and the reason it ended the loop (null when the loop decides again).
/// </summary>
internal sealed record ReengagePassResult(
    ReengageState State, bool ReportWritten, string? Stop,
    ReengageOutcome Outcome = ReengageOutcome.Continue);
