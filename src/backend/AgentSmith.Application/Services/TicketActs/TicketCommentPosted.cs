namespace AgentSmith.Application.Services.TicketActs;

/// <summary>2026-11-17-e061c: what one comment did — posted, or the first line of why not.</summary>
public sealed record TicketCommentPosted(string TicketId, string? Error)
{
    public bool Posted => Error is null;
}
