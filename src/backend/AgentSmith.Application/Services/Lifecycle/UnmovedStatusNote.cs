using System.Net;

namespace AgentSmith.Application.Services.Lifecycle;

/// <summary>
/// 2026-11-18-d6d2a / 550aa: the line a ticket comment carries when its status could not be set — posted
/// comment-only after the tracker rejected the move (Azure DevOps loses the comment with the state) or the
/// write threw, so the ticket says why it stays where it is. <paramref name="field"/> names the configured
/// status (failed_status, done_status).
/// </summary>
public static class UnmovedStatusNote
{
    public static string Append(string comment, string field, string status, string reason) =>
        comment + $"<br/><b>Status:</b> {field} '{WebUtility.HtmlEncode(status)}' could not be set: "
        + $"{WebUtility.HtmlEncode(reason)}. The ticket stays in its current status; correct the status on the "
        + "tracker or the project, or start it again with Retry.";
}
