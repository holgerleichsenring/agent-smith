using System.Globalization;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Prompts;

/// <summary>
/// p0317: renders the ticket's comment thread as a delimited "Ticket conversation"
/// prompt section — chronological, author-attributed, inside the p0316 untrusted-data
/// markers so an injection inside a comment reads as data, not a command. Empty
/// string when there is no conversation, so callers can interpolate unconditionally.
/// </summary>
public static class TicketConversationPromptSection
{
    public static string Render(IReadOnlyList<TicketComment>? comments)
    {
        if (comments is null || comments.Count == 0) return string.Empty;
        var thread = string.Join("\n\n", comments
            .OrderBy(c => c.CreatedAt)
            .Select(Format));
        return TicketPromptDelimiters.WrapSection("## Ticket conversation", thread);
    }

    private static string Format(TicketComment comment) =>
        $"[{comment.CreatedAt.ToString("u", CultureInfo.InvariantCulture)}] {comment.Author}:\n{comment.Body}";
}
