using AgentSmith.Application.Services.Specs;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;
using Sources = AgentSmith.Server.Models.TicketConversationSources;

namespace AgentSmith.Server.Services.Tickets.Conversation;

/// <summary>
/// 2026-11-17-dd05a: the cut comment a run posts on the ticket — every spec's criteria, facts and assumptions — reads
/// on What happened as one line: how many specs, derived or approved, and the revision. The full text is the Specs
/// section's. Only a comment whose FIRST line carries the cut heading is ours whole; a reply quoting it opens with the
/// quote. The revision is the attempt's only when the comment falls inside that attempt's run — a spec-derive run's
/// comment is placed under an earlier attempt it does not belong to.
/// </summary>
public static class SpecSetCommentLine
{
    /// <param name="attempt">The attempt row of the run the entry is placed under, if any.</param>
    /// <param name="run">That run, whose window decides whether its revision is the comment's.</param>
    public static TicketConversationEntryView Of(TicketConversationEntryView entry, RunAttempt? attempt, Run? run)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (!entry.IsOurs || entry.Source != Sources.Ticket || entry.Text is not { } text) return entry;
        var plain = entry.Format == TicketTextFormat.Html ? TicketHtmlConverter.ToText(text) : text;
        if (!OpensWithCut(plain)) return entry;
        var line = Line(plain, Inside(entry.At, run) ? attempt : null);
        return entry with { Kind = TicketConversationKinds.SpecSet, Text = line, Format = TicketTextFormat.Text };
    }

    private static string Line(string text, RunAttempt? attempt)
    {
        var count = Count(text, SpecSetComment.DoneHeading);
        var approved = text.Contains(SpecSetCommentPreamble.ApprovedOpening, StringComparison.OrdinalIgnoreCase);
        var how = !approved ? "derived"
            : string.IsNullOrEmpty(attempt?.ApprovalConversation) ? "approved" : "approved from Work it out";
        var revision = attempt?.RevisionNumber is { } number ? $", revision {number}" : string.Empty;
        return $"{count} {(count == 1 ? "spec" : "specs")} {how}{revision}";
    }

    private static bool OpensWithCut(string text) =>
        text.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0 && !l.StartsWith("<!--", StringComparison.Ordinal))
            is { } first && first.Contains(SpecSetComment.CutMarker, StringComparison.OrdinalIgnoreCase);

    private static bool Inside(DateTimeOffset at, Run? run) =>
        run is not null && at >= run.StartedAt && (run.FinishedAt is not { } end || at <= end);

    private static int Count(string text, string phrase)
    {
        var count = 0;
        for (var i = text.IndexOf(phrase, StringComparison.OrdinalIgnoreCase); i >= 0;
             i = text.IndexOf(phrase, i + phrase.Length, StringComparison.OrdinalIgnoreCase))
            count++;
        return count;
    }
}
