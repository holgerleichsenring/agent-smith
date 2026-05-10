using System.Text;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets.OpenQuestions;

/// <summary>
/// Jira variant. JiraTicketProvider posts comments as a single ADF text node
/// today, so HTML-comment anchors would render as literal text. Use visible
/// bracket anchors (`[Q1]`) and a visible leading marker so the parser can
/// detect the comment shape on the way back without surprising operators.
/// Multi-paragraph ADF marshalling is a follow-up; line breaks rendered as
/// plain text suffice for the round-trip.
/// </summary>
public sealed class JiraOpenQuestionsCommentTemplate : ITicketCommentTemplate
{
    public string Render(IReadOnlyList<PlanOpenQuestion> questions)
    {
        var sb = new StringBuilder();
        sb.AppendLine(OpenQuestionsCommentMarkers.PlainTextLeadingMarker);
        sb.AppendLine("Agent Smith — open questions");
        sb.AppendLine();
        sb.AppendLine("Reply with one line per question, e.g. `Q1: option-a`.");
        sb.AppendLine();
        foreach (var question in questions)
            AppendQuestion(sb, question);
        return sb.ToString().TrimEnd();
    }

    private static void AppendQuestion(StringBuilder sb, PlanOpenQuestion question)
    {
        sb.Append(OpenQuestionsCommentMarkers.PlainTextQuestionAnchor(question.Id));
        sb.Append(' ');
        sb.AppendLine($"Q{question.Id}: {question.Question}");
        if (question.Options.Count > 0)
            sb.AppendLine($"Options: {string.Join(", ", question.Options)}");
        sb.AppendLine();
    }
}
