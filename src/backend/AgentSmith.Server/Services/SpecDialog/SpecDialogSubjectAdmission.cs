using AgentSmith.Infrastructure.Persistence.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-20-4b0af: whether a minted conversation subject is stored at all.
/// <para>
/// A cap is not an admission rule. The subject is minted ONCE and never revised, so an answer
/// that arrives as three sentences, a fenced block, a bullet or a quoted string would leave a
/// permanently mangled heading over the conversation — truncating it to the column width would
/// only make it a shorter mangled heading. So an answer is ADMITTED or DISCARDED: one line of
/// plain prose, non-empty after trimming, within the column's length. Anything else stores
/// nothing and the heading falls back to the first line the person wrote, which is exactly what
/// it showed before this existed and therefore a safe failure.
/// </para>
/// </summary>
internal static class SpecDialogSubjectAdmission
{
    /// <summary>Marks that make an answer markup rather than a sentence, wherever they stand.</summary>
    private const string Markup = "`*_#[]{}<>|";

    /// <summary>Marks that make an answer a quotation or a list item when it OPENS with one.</summary>
    private const string Opening = "\"'-+•“„«";

    /// <summary>The answer as it would be stored, or null when it is not one line of prose.</summary>
    internal static string? Of(string? answer)
    {
        if (answer is null) return null;
        var line = answer.Trim();
        if (line.Length == 0 || line.Length > PersistenceLimits.ConversationSubject) return null;
        // A paragraph, a fenced block and a bulleted list all arrive with a line break in them.
        if (line.Any(char.IsControl)) return null;
        if (line.Any(Markup.Contains)) return null;
        if (Opening.Contains(line[0])) return null;
        // "1. The widget that reads the ledger" is a numbered list, not a subject.
        return char.IsDigit(line[0]) && line.SkipWhile(char.IsDigit).FirstOrDefault() is '.' or ')'
            ? null
            : line;
    }
}
