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
    /// <param name="waitingLine">
    /// p0454: who the comment waits for, already in the platform's mention form (see
    /// <see cref="TicketMention"/>) — an open-questions comment always waits, so it
    /// always carries one.
    /// </param>
    string Render(IReadOnlyList<PlanOpenQuestion> questions, string waitingLine);
}
