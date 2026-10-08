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

    private const string Rule = "Comments on this pull request are collected; only Request changes starts a rework.";

    public static string PrStarted(string ticketId, string? runId) => runId is null
        ? $"{PrMarker}\nAgent Smith — ticket {ticketId} is already taken or queued for its next attempt, which reads this review. {Rule}"
        : $"{PrMarker}\nAgent Smith — rework of ticket {ticketId} started as run `{runId}`. {Rule}";

    // 2026-10-08-0781: a live run's acts are served when it ends — nobody has to ask again.
    public static string PrLiveRun(string ticketId, string runId) =>
        $"{PrMarker}\nAgent Smith — run `{runId}` is working on ticket {ticketId}; this review is picked up when it finishes.";

    public static string TicketLiveRun(string runId) =>
        $"Agent Smith — run `{runId}` is working on this ticket; this comment is picked up when it finishes.";

    public static string PrWithheld(string ticketId, string runId) =>
        $"{PrMarker}\nAgent Smith — this review was written before an operator stopped run `{runId}` on ticket {ticketId}, so it is not picked up. Request changes again to start a rework.";

    public static string TicketWithheld(string runId) =>
        $"Agent Smith — this comment was written before an operator stopped run `{runId}`, so it is not picked up. Comment again to start a rework.";

    public static string PrRefused(string ticketId, string reason) =>
        $"{PrMarker}\nAgent Smith — this review did not start a rework of ticket {ticketId}: {reason}.";
}
