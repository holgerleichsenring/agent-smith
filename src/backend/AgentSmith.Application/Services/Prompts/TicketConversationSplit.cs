using System.Globalization;
using AgentSmith.Contracts.Runs;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Prompts;

/// <summary>
/// 2026-10-08-7c0e: the ticket conversation split at the previous attempt's start. Relevance is
/// decided over the WHOLE thread first — whether a comment of ours stays depends on its neighbour,
/// and the split must not cut a question from its answer. The foreign comments written after the
/// start (plus the skew margin) lead under their own heading; the rest follows, chronological, in whole entries within what the new block left.
/// Both share <see cref="TicketConversationPromptSection.MaxChars"/>, the new block fitted first,
/// so the operator's latest word is the one never dropped.
/// </summary>
internal static class TicketConversationSplit
{
    /// <summary>The split section, or null when nothing foreign was written since the start.</summary>
    public static string? Render(IReadOnlyList<TicketComment>? comments, PreviousAttempt attempt)
    {
        if (comments is null || comments.Count == 0) return null;
        var kept = TicketConversationPromptSection.Relevant([.. comments.OrderBy(c => c.CreatedAt)]);
        var fresh = kept.Where(c => !OwnTicketComment.IsOurs(c) && attempt.Precedes(c.CreatedAt)).ToList();
        if (fresh.Count == 0) return null;

        var newFit = NewestFirstFit.Of([.. fresh.Select(TicketConversationPromptSection.Format)],
            TicketConversationPromptSection.MaxChars, mayOvershoot: true);
        var remaining = TicketConversationPromptSection.MaxChars - newFit.Kept.Sum(t => t.Length);
        var earlier = kept.Except(fresh).Select(TicketConversationPromptSection.Format).ToList();
        var earlierFit = WholeEntriesWithin(earlier, remaining);
        return TicketPromptDelimiters.WrapSection("## Ticket conversation",
            Compose(attempt, newFit, earlierFit));
    }

    // The earlier part never overshoots and never shows half a comment: an entry that would have
    // to be cut is dropped, and with it everything older, which cannot fit either.
    private static FittedEntries WholeEntriesWithin(IReadOnlyList<string> earlier, int remaining)
    {
        if (remaining <= 0) return new FittedEntries([], earlier.Count, false);
        var fitted = NewestFirstFit.Of(earlier, remaining, mayOvershoot: false);
        return fitted.NewestCut ? new FittedEntries([], earlier.Count, false) : fitted;
    }

    private static string Compose(PreviousAttempt attempt, FittedEntries fresh, FittedEntries earlier)
    {
        // 2026-10-08-0781: the split point is what that run read, not when it started.
        var read = attempt.Cutoff.ToString("u", CultureInfo.InvariantCulture);
        var text = $"### Since the previous attempt (run {attempt.RunId}, read {read})\n\n"
            + string.Join("\n\n", fresh.Kept);
        if (earlier.Kept.Count > 0)
            text += "\n\n### Earlier in the thread\n\n" + string.Join("\n\n", earlier.Kept);
        var dropped = fresh.Dropped + earlier.Dropped;
        return dropped == 0 ? text
            : text + $"\n\n[{dropped} comment(s) omitted to fit — ask for the full history if you need it.]";
    }
}
