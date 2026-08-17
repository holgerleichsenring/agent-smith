using System.Globalization;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Prompts;

/// <summary>
/// p0424: renders the ticket's comment thread as a delimited "Ticket conversation"
/// prompt section — chronological, author-attributed, inside the p0316 untrusted-data
/// markers so an injection inside a comment reads as data, not a command.
/// <para>
/// It renders what the OPERATOR said. Ticket 19106's thread had grown to 147,462
/// characters — 97% of a 152k user message, re-sent on every one of 115 rounds — and
/// almost all of it was agent-smith's own announcements: spec handbacks, "this is how I
/// understood the ticket", open questions, run results from twenty-four runs. An agent
/// reading its own past output as if it were instruction spends its rounds reconciling
/// versions of the task instead of doing it: that phase made 348 tool calls and wrote
/// nothing in four hours.
/// </para>
/// <para>
/// Our own comments are kept only where they carry meaning for the operator's words: a
/// question the operator then answered, and the most recent one. Everything else of ours
/// is our own echo. What is dropped is stated, because a silently shortened thread is the
/// kind of missing context nobody can debug.
/// </para>
/// </summary>
public static class TicketConversationPromptSection
{
    /// <summary>
    /// A thread longer than this is the ticket's history, not its instruction. The most
    /// RECENT comments are kept: the operator's latest word is the one in force.
    /// </summary>
    public const int MaxChars = 20_000;

    private static readonly string[] OwnCommentMarkers =
    [
        "agent-smith:open-questions",
        "[agent-smith open questions]",
        "Agent Smith —",
        "Agent Smith &#8212;",
    ];

    public static string Render(IReadOnlyList<TicketComment>? comments)
    {
        if (comments is null || comments.Count == 0) return string.Empty;

        var ordered = comments.OrderBy(c => c.CreatedAt).ToList();
        var kept = Relevant(ordered);
        var (thread, dropped) = Fit(kept);
        if (thread.Length == 0) return string.Empty;

        var note = dropped == 0
            ? string.Empty
            : $"\n\n[{dropped} earlier comment(s) omitted — this is the recent thread. "
                + "Ask for the full history if you need it.]";
        return TicketPromptDelimiters.WrapSection("## Ticket conversation", thread + note);
    }

    /// <summary>
    /// The operator's comments, plus the two kinds of ours that carry meaning for them:
    /// a question that was answered, and the latest one.
    /// </summary>
    private static IReadOnlyList<TicketComment> Relevant(IReadOnlyList<TicketComment> ordered)
    {
        var kept = new List<TicketComment>();
        for (var i = 0; i < ordered.Count; i++)
        {
            var comment = ordered[i];
            if (!IsOurs(comment)) { kept.Add(comment); continue; }
            var answered = i + 1 < ordered.Count && !IsOurs(ordered[i + 1]);
            if (answered || i == ordered.Count - 1) kept.Add(comment);
        }
        return kept;
    }

    private static bool IsOurs(TicketComment comment) =>
        OwnCommentMarkers.Any(marker =>
            comment.Body?.Contains(marker, StringComparison.OrdinalIgnoreCase) == true);

    private static (string Thread, int Dropped) Fit(IReadOnlyList<TicketComment> comments)
    {
        var taken = new List<string>();
        var length = 0;
        var dropped = 0;
        foreach (var comment in comments.Reverse())
        {
            var text = Format(comment);
            if (length + text.Length > MaxChars && taken.Count > 0) { dropped++; continue; }
            taken.Insert(0, text);
            length += text.Length;
        }
        return (string.Join("\n\n", taken), dropped);
    }

    private static string Format(TicketComment comment) =>
        $"[{comment.CreatedAt.ToString("u", CultureInfo.InvariantCulture)}] {comment.Author}:\n{comment.Body}";
}
