namespace AgentSmith.Application.Services.TicketActs;

/// <summary>2026-11-17-e061c: one comment a 'comment' run posts — rendered by the server, headed as agent-smith's own so
/// no webhook takes it for a person's.</summary>
public sealed record TicketCommentToPost(string TicketId, string Text);
