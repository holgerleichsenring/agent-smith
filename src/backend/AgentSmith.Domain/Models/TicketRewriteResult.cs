namespace AgentSmith.Domain.Models;

/// <summary>
/// 2026-09-25-8e51e: the tracker's answer to rewriting the framework's region of a ticket body.
/// Anything but <see cref="TicketRewriteOutcome.Rewritten"/> carries the reason a person reads,
/// because this write is the one a conversation promised somebody — and it never throws: a
/// tracker that cannot carry the rewrite is an answer, not an exception.
/// </summary>
public sealed record TicketRewriteResult(TicketRewriteOutcome Outcome, string? Reason)
{
    public bool Rewritten => Outcome == TicketRewriteOutcome.Rewritten;

    public static TicketRewriteResult Ok { get; } = new(TicketRewriteOutcome.Rewritten, null);

    public static TicketRewriteResult Unsupported(string reason) =>
        new(TicketRewriteOutcome.Unsupported, reason);

    public static TicketRewriteResult Failed(string reason) => new(TicketRewriteOutcome.Failed, reason);
}
