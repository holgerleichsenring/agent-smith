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
    /// <param name="answerLink">
    /// p0461: the run's ratification surface, or null when this deployment has no
    /// configured dashboard address — the body said "ratify it on the run's dashboard
    /// prompt" without ever saying where that was.
    /// </param>
    string Render(ExpectationDraft draft, string waitingLine, string? answerLink);
}
