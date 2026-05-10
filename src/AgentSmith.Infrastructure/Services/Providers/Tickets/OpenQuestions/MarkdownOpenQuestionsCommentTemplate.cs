using System.Text;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets.OpenQuestions;

/// <summary>
/// Default markdown body for an open-questions comment. Used by GitHub /
/// GitLab / Azure DevOps where HTML-comment anchors are preserved in the
/// raw markdown source the webhook delivers back. Each question becomes a
/// `<!--Q{id}--> **Q{id}:** ...` block.
/// </summary>
public class MarkdownOpenQuestionsCommentTemplate : ITicketCommentTemplate
{
    public string Render(IReadOnlyList<PlanOpenQuestion> questions)
    {
        var sb = new StringBuilder();
        sb.AppendLine(OpenQuestionsCommentMarkers.MarkdownLeadingMarker);
        sb.AppendLine("**Agent Smith — open questions**");
        sb.AppendLine();
        sb.AppendLine("Reply to this comment with one line per question, e.g. `Q1: option-a`.");
        sb.AppendLine();
        foreach (var question in questions)
            AppendQuestion(sb, question);
        return sb.ToString().TrimEnd();
    }

    private static void AppendQuestion(StringBuilder sb, PlanOpenQuestion question)
    {
        sb.AppendLine(OpenQuestionsCommentMarkers.MarkdownQuestionAnchor(question.Id));
        sb.AppendLine($"**Q{question.Id}:** {question.Question}");
        if (question.Options.Count > 0)
            sb.AppendLine($"Options: {string.Join(", ", question.Options)}");
        sb.AppendLine();
    }
}
