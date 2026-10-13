namespace AgentSmith.Server.Services.Rework;

/// <summary>
/// 2026-10-08-e8b9b: what a refused rework says. Every text opens with our heading, so the comment
/// handlers read it as ours, and none quotes the configured keyword.
/// </summary>
public static class ReworkTexts
{
    public static string TicketRefusal(string reason) =>
        $"Agent Smith — this comment did not start a rework: {reason}.";

    /// <summary>2026-10-08-e8b9c: our PR comments open with this marker, so the review reader keeps them
    /// apart from what reviewers wrote.</summary>
    public const string PrMarker = "<!-- agentsmith:rework -->";

    private const string Rule =
        "Comments on this pull request are collected; a comment `/agent-smith` or a Request changes reworks all of them.";

    public static string PrStarted(string ticketId, string? runId) => runId is null
        ? $"{PrMarker}\nAgent Smith — ticket {ticketId} is already taken or queued for its next attempt, which reads this review. {Rule}"
        : $"{PrMarker}\nAgent Smith — rework of ticket {ticketId} started as run `{runId}`. {Rule}";

    // 2026-10-08-0781: a live run's acts are served when it ends — nobody has to ask again.
    public static string PrLiveRun(string ticketId, string runId) =>
        $"{PrMarker}\nAgent Smith — run `{runId}` is working on ticket {ticketId}; this review is picked up when it finishes.";

    public static string TicketLiveRun(string runId) =>
        $"Agent Smith — run `{runId}` is working on this ticket; this comment is picked up when it finishes.";

    public static string PrWithheld(string ticketId, string runId) =>
        $"{PrMarker}\nAgent Smith — this review was written before an operator stopped run `{runId}` on ticket {ticketId}, so it is not picked up. Comment `/agent-smith` or Request changes again to start a rework.";

    public static string TicketWithheld(string runId) =>
        $"Agent Smith — this comment was written before an operator stopped run `{runId}`, so it is not picked up. Comment again to start a rework.";

    public static string PrRefused(string ticketId, string reason) =>
        $"{PrMarker}\nAgent Smith — this review did not start a rework of ticket {ticketId}: {reason}.";

    // 2026-10-13-9a59t: a ticket a person closed is not reopened; its act is followed up on a new ticket.
    public static string PrFollowedUp(string ticketId, string followUp) =>
        $"{PrMarker}\nAgent Smith — ticket {ticketId} was closed by a person, so this review did not reopen it: it continues on {followUp}.";

    public const string NotWaiting =
        "the ticket is not waiting for one — its last run has not finished, or its current status is not one a rework starts from";

    public const string ClosedRefusal =
        "the ticket was closed in its tracker — approve a change in its conversation to file a follow-up";

    public static string FollowUpBody(string text, string closedTicket, string author) =>
        $"{text}\n\nAgent Smith — follows up {closedTicket}, which a person closed; {author} asked for this there.";
}
