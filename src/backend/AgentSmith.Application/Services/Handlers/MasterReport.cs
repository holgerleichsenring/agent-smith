using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-22-96c8a: a coding master's report is what it said as it ended its passes. A pass's ending text is the text
/// of its final message (an assistant message; text beside a tool call counts only there). Within one master step the
/// report is every pass's ending text in order, joined with a blank line, an identical text once — so a re-drive's
/// short closing line adds to the report instead of replacing it, and nothing pass 0 said is lost. A pass with no
/// ending text (the budget, an exception, a final message that is only a call or empty) adds nothing. Nothing here
/// is parsed.
/// </summary>
internal static class MasterReport
{
    internal const string Separator = "\n\n";

    /// <summary>The text this pass ended on, or null.</summary>
    internal static string? OfPass(ChatResponse? response)
    {
        if (response?.Messages is not { Count: > 0 } messages) return null;
        var last = messages[^1];
        if (last.Role != ChatRole.Assistant || string.IsNullOrWhiteSpace(last.Text)) return null;
        return last.Text.Trim();
    }

    /// <summary>The step's ending texts after a pass: its own appended when it ended on one not said before.</summary>
    internal static IReadOnlyList<string> Append(IReadOnlyList<string> texts, ChatResponse? response) =>
        OfPass(response) is { } text && !texts.Contains(text, StringComparer.Ordinal) ? [.. texts, text] : texts;

    /// <summary>
    /// The spec's report after a step: its earlier report (an earlier step of the same spec — the first pass before a
    /// stage repair, a structure correction or a review fix), then this step's ending texts not already in it as a
    /// whole part. Null when neither has text.
    /// </summary>
    internal static string? After(string? earlier, IReadOnlyList<string> texts)
    {
        if (earlier is null) return Join(texts);
        var added = texts.Where(t => !IsPart(earlier, t)).ToList();
        return added.Count == 0 ? earlier : earlier + Separator + Join(added);
    }

    private static bool IsPart(string joined, string text) =>
        joined == text || joined.StartsWith(text + Separator, StringComparison.Ordinal)
        || joined.EndsWith(Separator + text, StringComparison.Ordinal)
        || joined.Contains(Separator + text + Separator, StringComparison.Ordinal);

    /// <summary>The report the step's ending texts make, or null when no pass ended on text.</summary>
    internal static string? Join(IReadOnlyList<string> texts) => texts.Count == 0 ? null : string.Join(Separator, texts);
}
