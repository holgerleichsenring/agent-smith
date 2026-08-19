using AgentSmith.Contracts.Expectations;

namespace AgentSmith.Contracts.Tickets;

/// <summary>
/// p0328: renders the ticket-comment body for a drafted expectation, per
/// platform — markdown platforms (GitHub/GitLab/AzDO) share the canonical
/// body; Jira renders a plain-text variant (comments POST as a single ADF
/// text node today), following the OpenQuestions template precedent.
/// </summary>
public interface IExpectationCommentTemplate
{
    /// <param name="waitingLine">
    /// p0454: who the ratification waits for, already in the platform's mention form
    /// (see <see cref="TicketMention"/>).
    /// </param>
    string Render(ExpectationDraft draft, string waitingLine);
}
