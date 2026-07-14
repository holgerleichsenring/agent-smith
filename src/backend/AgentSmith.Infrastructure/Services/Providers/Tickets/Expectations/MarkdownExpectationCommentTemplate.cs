using System.Text;
using AgentSmith.Contracts.Expectations;
using AgentSmith.Contracts.Tickets;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets.Expectations;

/// <summary>
/// p0328: canonical markdown body for the expectation ticket comment — GitHub /
/// GitLab / Azure DevOps (HTML-comment anchors survive in raw markdown). The
/// block itself is the shared <see cref="ExpectationMarkdown"/> rendering, so a
/// reply that edits it parses back into the schema.
/// </summary>
public class MarkdownExpectationCommentTemplate : IExpectationCommentTemplate
{
    public string Render(ExpectationDraft draft)
    {
        var sb = new StringBuilder();
        sb.AppendLine(ExpectationCommentMarkers.MarkdownLeadingMarker);
        sb.AppendLine("**Agent Smith — expectation to ratify**");
        sb.AppendLine();
        sb.AppendLine("This is what the agent understood and will implement. Ratify it on the "
                      + "run's dashboard/chat prompt: reply `approve`, `reject`, or an edited "
                      + "version of the block.");
        sb.AppendLine();
        sb.AppendLine(ExpectationMarkdown.Render(draft));
        return sb.ToString().TrimEnd();
    }
}
