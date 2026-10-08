namespace AgentSmith.Server.Services.Rework;

/// <summary>
/// 2026-10-08-e8b9b: what a refused rework says. Every text opens with our heading, so the comment
/// handlers read it as ours, and none quotes the configured keyword.
/// </summary>
public static class ReworkTexts
{
    public static string TicketRefusal(string reason) =>
        $"Agent Smith — this comment did not start a rework: {reason}.";
}
