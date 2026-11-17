namespace AgentSmith.Application.Services.TicketActs;

/// <summary>2026-11-17-e061c: what a 'comment' run posts, in order, and whether the open pages of each posted ticket are
/// told it changed.</summary>
public sealed record PostTicketCommentsRequest(IReadOnlyList<TicketCommentToPost> Comments, bool NudgeActivity);
