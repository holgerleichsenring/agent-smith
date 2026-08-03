using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0393a: the ticket comment a hand-back posts. The contradiction case carries the
/// p0318 anchors, so an operator's reply parses back as an answer and re-triggers
/// the run. The VERDICT case carries none on purpose: without an anchor no comment
/// can be read as an answer, so commenting cannot restart a not-implementable
/// ticket — only an explicit operator Retry can.
/// </summary>
public static class SpecHandbackComment
{
    public static string Build(SpecHandback handback, string? prUrl)
    {
        ArgumentNullException.ThrowIfNull(handback);
        var spec = prUrl is null ? string.Empty : $"\n\nThe derived spec is open for review: {prUrl}";
        return handback.Case switch
        {
            SpecHandbackCase.NotImplementable =>
                "## Agent Smith — not implementable as specified\n\n"
                + handback.Reason
                + "\n\nThis is a verdict, not a question: a comment will not restart the work. "
                + "Change the ticket and use Retry on the run when it should be attempted again."
                + spec,
            _ =>
                "## Agent Smith — the requirement contradicts what is in the repository\n\n"
                + handback.Reason + spec,
        };
    }
}
