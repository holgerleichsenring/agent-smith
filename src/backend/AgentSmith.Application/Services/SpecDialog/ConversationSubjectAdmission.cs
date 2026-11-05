using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-09-20-4b0af: whether a conversation subject is stored at all.
/// <para>
/// A cap is not an admission rule. A stored subject is never revised, so a name that arrives as three sentences, a
/// fenced block, a bullet or a quoted string would leave a permanently mangled heading over the conversation —
/// truncating it to the column width would only make it a shorter mangled heading. So a name is ADMITTED or
/// REFUSED: one line of plain prose, non-empty after trimming, within the column's length.
/// </para>
/// <para>
/// 2026-11-04-96a0a: moved here from the server and run inside name_conversation, so the refusal names the rule that
/// failed and goes back to the model, which can name the conversation again.
/// </para>
/// </summary>
public static class ConversationSubjectAdmission
{
    /// <summary>Marks that make a name markup rather than a sentence, wherever they stand.</summary>
    private const string Markup = "`*_#[]{}<>|";

    /// <summary>Marks that make a name a quotation or a list item when it OPENS with one.</summary>
    private const string Opening = "\"'-+•“„«";

    /// <summary>The name as it would be stored, or the rule it broke.</summary>
    public static ConversationSubjectVerdict Of(string? name)
    {
        var line = name?.Trim() ?? string.Empty;
        return RuleBroken(line) is { } rule
            ? ConversationSubjectVerdict.Refused(rule)
            : ConversationSubjectVerdict.Admitted(line);
    }

    private static string? RuleBroken(string line)
    {
        if (line.Length == 0) return "The name is empty.";
        if (line.Length > ConversationSubjectLimits.Length)
            return $"The name is longer than {ConversationSubjectLimits.Length} characters.";
        // A paragraph, a fenced block and a bulleted list all arrive with a line break in them.
        if (line.Any(char.IsControl)) return "The name is more than one line.";
        if (line.Any(Markup.Contains)) return $"The name contains markup ({Markup}).";
        if (Opening.Contains(line[0])) return "The name opens like a quotation or a list item.";
        // "1. The widget that reads the ledger" is a numbered list, not a subject.
        return char.IsDigit(line[0]) && line.SkipWhile(char.IsDigit).FirstOrDefault() is '.' or ')'
            ? "The name opens like a numbered list item."
            : null;
    }
}
