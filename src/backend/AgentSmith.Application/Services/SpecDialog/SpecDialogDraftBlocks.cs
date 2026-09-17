using System.Text.RegularExpressions;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// The two fenced shapes a design-partner reply proposes work in: a bare ```yaml block is a
/// phase draft, an ```outcome block a bug or an epic. The resolver and the validator read
/// their blocks through these rules, and a surface that shows a reply without its draft
/// finds the draft through the same ones — so "what counts as a draft" has one answer.
/// <para>
/// Presence, not validity, is what the strip needs: an invalid draft is re-prompted once and
/// then replaced by a notice, so a reply that reaches a person carries one valid block or
/// none.
/// </para>
/// </summary>
public static partial class SpecDialogDraftBlocks
{
    [GeneratedRegex("```outcome\\s*\\n(.*?)```", RegexOptions.Singleline)]
    public static partial Regex OutcomeBlock();

    [GeneratedRegex("```yaml\\s*\\n(.*?)```", RegexOptions.Singleline)]
    public static partial Regex YamlBlock();

    [GeneratedRegex("```(?:outcome|yaml)\\s*\\n.*?```", RegexOptions.Singleline)]
    private static partial Regex AnyDraftBlock();

    [GeneratedRegex("\\n{3,}")]
    private static partial Regex BlankRun();

    public static bool Contains(string? reply) =>
        !string.IsNullOrEmpty(reply) && AnyDraftBlock().IsMatch(reply);

    /// <summary>
    /// The reply as prose: every draft block removed, the blank lines it leaves collapsed.
    /// A reply that was nothing but a draft becomes empty.
    /// </summary>
    public static string Strip(string? reply)
    {
        if (!Contains(reply)) return reply ?? string.Empty;
        var prose = AnyDraftBlock().Replace(reply!, string.Empty);
        return BlankRun().Replace(prose, "\n\n").Trim();
    }
}
