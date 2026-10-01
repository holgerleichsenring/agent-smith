using System.Text;
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
/// <para>
/// 2026-10-01-aeb6b: a ````document fence (four backticks) is a text the operator asked for to
/// take elsewhere, and a hand-off prompt routinely carries a ```yaml example. Nothing inside a
/// document is a draft: every rule here reads the reply with its documents blanked, and the
/// strip leaves them where they stood.
/// </para>
/// </summary>
public static partial class SpecDialogDraftBlocks
{
    [GeneratedRegex("```outcome\\s*\\n(.*?)```", RegexOptions.Singleline)]
    private static partial Regex OutcomeBlock();

    [GeneratedRegex("```yaml\\s*\\n(.*?)```", RegexOptions.Singleline)]
    private static partial Regex YamlBlock();

    [GeneratedRegex("````document[^\\n]*\\n.*?\\n````", RegexOptions.Singleline)]
    private static partial Regex DocumentBlock();

    [GeneratedRegex("```(?:outcome|yaml)\\s*\\n.*?```", RegexOptions.Singleline)]
    private static partial Regex AnyDraftBlock();

    [GeneratedRegex("\\n{3,}")]
    private static partial Regex BlankRun();

    /// <summary>The ```outcome blocks of a reply, outside its documents.</summary>
    public static MatchCollection OutcomeBlocks(string? reply) => OutcomeBlock().Matches(OutsideDocuments(reply));

    /// <summary>The ```yaml blocks of a reply, outside its documents.</summary>
    public static MatchCollection YamlBlocks(string? reply) => YamlBlock().Matches(OutsideDocuments(reply));

    public static bool Contains(string? reply) =>
        !string.IsNullOrEmpty(reply) && AnyDraftBlock().IsMatch(OutsideDocuments(reply));

    private static string OutsideDocuments(string? reply) =>
        DocumentBlock().Replace(reply ?? string.Empty, string.Empty);

    /// <summary>
    /// The reply as prose: every draft block removed, the blank lines it leaves collapsed.
    /// A reply that was nothing but a draft becomes empty.
    /// </summary>
    public static string Strip(string? reply)
    {
        if (!Contains(reply)) return reply ?? string.Empty;
        var shown = new StringBuilder();
        var at = 0;
        foreach (Match document in DocumentBlock().Matches(reply!))
        {
            shown.Append(StripDrafts(reply![at..document.Index])).Append(document.Value);
            at = document.Index + document.Length;
        }
        return shown.Append(StripDrafts(reply![at..])).ToString().Trim();
    }

    private static string StripDrafts(string prose) =>
        BlankRun().Replace(AnyDraftBlock().Replace(prose, string.Empty), "\n\n");
}
