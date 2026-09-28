namespace AgentSmith.Application.Services.Prompts;

/// <summary>
/// 2026-09-27-481ba: fitting the END of a thread into a budget — whole entries, newest first,
/// reporting what did not fit.
/// <para>
/// Extracted from <see cref="TicketConversationPromptSection"/> once the design path needed the
/// same rule. It takes entries ALREADY ORDERED and ALREADY RENDERED, because the two callers
/// disagree about both and the disagreements are load-bearing: the run path stamps a timestamp on
/// each comment and the design path does not, and the run path's filter reads each comment's
/// NEIGHBOUR — so it must sort before it filters, and a fit that sorted afterwards would be
/// sorting what the filter has already consumed.
/// </para>
/// <para>
/// OVERSHOOTING IS THE CALLER'S CHOICE, and it is the one place the two paths must differ. The run
/// path takes the newest entry whole however large it is: its budget bounds a prompt, and dropping
/// the most recent thing anybody said would be worse than exceeding it. The design path cannot —
/// its text is stored in a column bounded at the same number, so an oversize entry is a write that
/// fails. It cuts instead, and is told that it did.
/// </para>
/// </summary>
internal static class NewestFirstFit
{
    /// <summary>
    /// The entries that fit, oldest first; how many did not; and whether the newest had to be cut
    /// to fit at all — which only happens when the caller forbids overshooting.
    /// </summary>
    public static FittedEntries Of(
        IReadOnlyList<string> ordered, int budget, bool mayOvershoot)
    {
        ArgumentNullException.ThrowIfNull(ordered);
        var taken = new List<string>();
        var length = 0;
        var dropped = 0;
        var cut = false;
        for (var i = ordered.Count - 1; i >= 0; i--)
        {
            var text = ordered[i];
            if (length + text.Length > budget)
            {
                // Everything after the first entry is dropped whole: a half-rendered comment reads
                // like a complete one, which is the failure this phase exists to remove.
                if (taken.Count > 0) { dropped++; continue; }
                if (!mayOvershoot)
                {
                    if (budget <= 0) { dropped++; continue; }
                    text = text[..budget];
                    cut = true;
                }
            }

            taken.Insert(0, text);
            length += text.Length;
        }

        return new FittedEntries(taken, dropped, cut);
    }
}

/// <summary>What fitted, what did not, and whether the newest entry was cut to make it fit.</summary>
internal sealed record FittedEntries(IReadOnlyList<string> Kept, int Dropped, bool NewestCut);
