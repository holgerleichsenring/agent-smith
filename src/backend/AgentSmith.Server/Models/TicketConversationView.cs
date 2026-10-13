namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-13-9a59h: a ticket's conversation in time order — what its runs recorded, its pull requests'
/// records, the live ticket thread and the bound Work it out conversation — each entry under its attempt.
/// </summary>
/// <param name="Attempts">The attempt markers, oldest first.</param>
/// <param name="SessionId">The bound Work it out conversation; null when none, or when it is hidden.</param>
/// <param name="OpenDialogId">The dialog that conversation lives on while it is open.</param>
/// <param name="Proposal">The open proposal; null when none is open, or when it is hidden.</param>
/// <param name="TranscriptHidden">True exactly when a bound conversation exists and the caller may not read it.</param>
/// <param name="ThreadUnavailable">The ticket's own thread could not be read from its tracker.</param>
public sealed record TicketConversationView(
    string Project,
    string TicketId,
    IReadOnlyList<TicketConversationEntryView> Entries,
    IReadOnlyList<TicketConversationAttemptView> Attempts,
    string? SessionId,
    string? OpenDialogId,
    TicketConversationProposalView? Proposal,
    bool TranscriptHidden,
    bool ThreadUnavailable);
