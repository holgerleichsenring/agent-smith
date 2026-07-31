using AgentSmith.Contracts.WorkSpecs;

namespace AgentSmith.Application.Services.WorkSpecs;

/// <summary>
/// p0390: the ticket comment a hand-back posts. The two question cases carry
/// the p0318 anchors, so an operator's reply parses back as an answer and
/// re-triggers the run. The VERDICT case carries none on purpose: without an
/// anchor no comment can be read as an answer, so commenting cannot restart a
/// not-implementable ticket — only an explicit operator Retry can.
/// </summary>
public static class WorkSpecHandbackComment
{
    public static string Build(WorkSpecHandback handback, string? prUrl)
    {
        ArgumentNullException.ThrowIfNull(handback);
        var spec = prUrl is null ? string.Empty : $"\n\nThe derived work spec is open for review: {prUrl}";
        return handback.Case switch
        {
            WorkSpecHandbackCase.NotImplementable =>
                "## Agent Smith — not implementable as specified\n\n"
                + handback.Reason
                + "\n\nThis is a verdict, not a question: a comment will not restart the work. "
                + "Change the ticket and use Retry on the run when it should be attempted again."
                + spec,
            WorkSpecHandbackCase.RequirementsDoNotMatchTheCode =>
                "## Agent Smith — the requirements do not match the code\n\n"
                + handback.Reason + spec,
            _ => "## Agent Smith — the ticket could not be read as a statement of work\n\n"
                + handback.Reason + spec,
        };
    }
}
