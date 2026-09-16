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
            : $"{ticket.Description}\n\n## Acceptance criteria\n{ticket.AcceptanceCriteria}";
    }
}
