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
    string Render(ExpectationDraft draft);
}
