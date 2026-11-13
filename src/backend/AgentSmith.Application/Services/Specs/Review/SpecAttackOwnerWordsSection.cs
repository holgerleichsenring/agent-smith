using System.Text;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-11-12-92e9b: the operator's own messages in a design conversation, oldest first, rendered after the open
/// questions and before the draft — as the ticket-list reviewer reads them. A decision the owner stated there is
/// taken; the design partner's side of the conversation is never shown. Empty with no messages.
/// </summary>
public static class SpecAttackOwnerWordsSection
{
    public const string Heading = "## The owner's own words (their messages in this conversation, oldest first)";

    public const string Rule =
        "A decision the owner states here is made: judge the spec as if it holds, and do not ask for it again or "
        + "object that it is unconfirmed.";

    public static string Render(IReadOnlyList<string> turns)
    {
        ArgumentNullException.ThrowIfNull(turns);
        if (turns.Count == 0) return string.Empty;
        var sb = new StringBuilder().AppendLine().AppendLine(Heading).AppendLine(Rule).AppendLine();
        return sb.AppendLine(string.Join("\n---\n", turns.Select(t => t.Trim()))).ToString();
    }
}
