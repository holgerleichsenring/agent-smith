using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-09-15-6d9c: the body a fix-bug ticket is filed with — description, and the
/// acceptance criteria under their own heading when the draft states any.
/// <para>
/// One composition, because the proposal pane shows what WOULD be filed: a second copy of
/// this shape would disagree with the ticket the first time either side gained a section.
/// </para>
/// </summary>
public sealed class BugTicketRenderer
{
    public string RenderBody(BugTicketDraft ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        return string.IsNullOrWhiteSpace(ticket.AcceptanceCriteria)
            ? ticket.Description
            : $"{ticket.Description}\n\n{AcceptanceCriteriaSection.Heading}\n{Listed(ticket.AcceptanceCriteria)}";
    }

    /// <summary>
    /// 2026-10-01-f5c3b: the criteria as list items, because only an item is read back as a
    /// criterion. A listed line and an indented line under it — the reader joins that to the
    /// item above — stay as written; every other non-empty line becomes one item of its own.
    /// </summary>
    private static string Listed(string criteria)
    {
        var lines = new List<string>();
        foreach (var line in criteria.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            if (line.Trim().Length == 0) continue;
            var keeps = CriterionLine.ItemText(line) is not null || (lines.Count > 0 && char.IsWhiteSpace(line[0]));
            lines.Add(keeps ? line.TrimEnd() : $"- {CriterionLine.Collapse(line)}");
        }
        return string.Join("\n", lines);
    }
}
