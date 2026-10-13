using AgentSmith.Contracts.Runs;

namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-13-9a59h: an attempt marker in a ticket's conversation — the run, its number, why it began and the
/// revision it worked. <paramref name="DemandEntryId"/> names the entry that demanded a re-cut.
/// </summary>
public sealed record TicketConversationAttemptView(
    string RunId,
    int Number,
    AttemptCause? Cause,
    TicketAttemptRevisionView? Revision,
    string? DemandEntryId = null);
