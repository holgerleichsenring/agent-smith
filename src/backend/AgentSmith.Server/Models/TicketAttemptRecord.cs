using AgentSmith.Contracts.Runs;

namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-13-9a59d: what an attempt's RunAttempt row says, read for the ticket views — why it began,
/// the revision it worked from, and the tracker state and address it saw at fetch.
/// </summary>
public sealed record TicketAttemptRecord(
    AttemptCause? Cause, TicketAttemptRevisionView? Revision, string? TrackerState, string? WebUrl);
