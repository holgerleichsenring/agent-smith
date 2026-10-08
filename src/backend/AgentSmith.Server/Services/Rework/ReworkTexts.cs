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

    public static string PrLiveRun(string ticketId, string runId, bool voteOnly) =>
        $"{PrMarker}\nAgent Smith — this review was not picked up: run `{runId}` is working on ticket {ticketId}. "
        + $"When run `{runId}` ends, submit Request changes again"
        + (voteOnly ? " (reset your vote first — only a change of vote is sent)." : ".");

    public static string PrRefused(string ticketId, string reason) =>
        $"{PrMarker}\nAgent Smith — this review did not start a rework of ticket {ticketId}: {reason}.";
}
