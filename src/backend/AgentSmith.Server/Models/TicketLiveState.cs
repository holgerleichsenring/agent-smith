using AgentSmith.Server.Services.Events;

namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-13-9a59a: what the run row cannot say about a parked or queued attempt — its place in
/// the capacity queue, whether its relaunch already holds a lease, and its pending question.
/// </summary>
public sealed record TicketLiveState(
    IReadOnlyDictionary<string, int> Positions,
    IReadOnlySet<string> Relaunching,
    IReadOnlyDictionary<string, PendingQuestionInfo> Pending);
