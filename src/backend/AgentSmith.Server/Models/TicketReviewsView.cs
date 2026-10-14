namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-13-8762l: every pre-build review round of a ticket — each code attempt's rounds, numbered as the
/// series read numbers its attempts, and the rounds the approved set was reviewed in during the design
/// conversation, from the approval record and already keyed by filed ids.
/// </summary>
/// <param name="Conversation">The approval record's rounds; null when the ticket has no approval record.</param>
public sealed record TicketReviewsView(
    string Project,
    string TicketId,
    IReadOnlyList<TicketReviewAttemptView> Attempts,
    TicketReviewConversationView? Conversation);

/// <summary>2026-10-13-8762l: the rounds the design conversation reviewed the approved set in, and when it was approved.</summary>
public sealed record TicketReviewConversationView(DateTimeOffset? ApprovalAt, IReadOnlyList<TicketReviewRoundView> Rounds);
