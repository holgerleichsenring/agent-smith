using System.Text;
using AgentSmith.Contracts.Expectations;
using AgentSmith.Contracts.Tickets;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets.Expectations;

/// <summary>
/// p0328: Jira variant — comments POST as a single ADF text node today, so the
/// HTML-comment anchor would render as literal text. Visible bracket marker +
/// the same canonical block (OpenQuestions template precedent).
/// </summary>
public sealed class JiraExpectationCommentTemplate : IExpectationCommentTemplate
{
    public string Render(ExpectationDraft draft)
    {
        var sb = new StringBuilder();
        sb.AppendLine(ExpectationCommentMarkers.PlainTextLeadingMarker);
        sb.AppendLine("Agent Smith — expectation to ratify");
        sb.AppendLine();
        sb.AppendLine("This is what the agent understood and will implement. Ratify it on the "
                      + "run's dashboard/chat prompt: reply approve, reject, or an edited "
                      + "version of the block.");
        sb.AppendLine();
        sb.AppendLine(ExpectationMarkdown.Render(draft));
        return sb.ToString().TrimEnd();
    }
}
