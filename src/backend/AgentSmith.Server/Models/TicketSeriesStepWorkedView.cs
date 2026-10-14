namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-13-9a59f: one spec step as the attempts worked it — its latest ledger state (null when no
/// attempt moved it), and while it stands done, the attempt and run-wide master pass that moved it there.
/// </summary>
public sealed record TicketSeriesStepWorkedView(
    string StepId, string? State, string? DoneByRunId, int? DoneByAttempt, int? DoneInPass);
