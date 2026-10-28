namespace AgentSmith.Server.Services.SpecDialog.Continuation;

/// <summary>2026-10-13-9a59l: what an approval in a ticket's own conversation does.</summary>
public static class TicketContinuationEffects
{
    /// <summary>A bug proposal files a fix-bug ticket, as it always did.</summary>
    public const string BugTicket = "bug-ticket";

    /// <summary>A person closed the ticket: the approval files one linked follow-up ticket.</summary>
    public const string FollowUpClosed = "follow-up-closed";

    /// <summary>No code attempt worked the ticket: the approved series replaces what was under it.</summary>
    public const string AmendNeverWorked = "amend-never-worked";

    /// <summary>The series would hold more specs than one ticket may.</summary>
    public const string RefusedOverCap = "refused-over-cap";

    /// <summary>The proposal cannot be decided at all — a cut whose edges have no order.</summary>
    public const string Refused = "refused";

    /// <summary>The ticket's next attempt works the series continued by spec id.</summary>
    public const string Continue = "continue";

    /// <summary>2026-10-28-7a69c: the approval posts a comment on the ticket and starts nothing.</summary>
    public const string Comment = "comment";
}
