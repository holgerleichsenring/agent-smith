using AgentSmith.Domain.Entities;

namespace AgentSmith.Contracts.Tickets;

/// <summary>
/// Renders a ticket-comment body for a list of Plan open questions. One
/// implementation per platform — markdown platforms (GitHub/GitLab/AzDO)
/// share the canonical body; Jira renders a plain-text variant since Jira
/// comments POST as a single ADF text node today (richer ADF marshalling
/// is a follow-up).
/// </summary>
public interface ITicketCommentTemplate
{
    string Render(IReadOnlyList<PlanOpenQuestion> questions);
}
