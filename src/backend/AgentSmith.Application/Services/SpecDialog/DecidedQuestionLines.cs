using System.Text;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-11-12-e2f5: the open questions a proposal still carried when its owner approved it. An answer the owner gave
/// was sent as a reply and re-drafted, so whatever stands at approval files on its proposal — and the run reads it as
/// decided, not as an open 'proposed:' choice.
/// </summary>
public static class DecidedQuestionLines
{
    public const string Heading = "## Decided by the owner";

    public static SpecTicketContent Appended(SpecTicketContent content, IReadOnlyList<OpenQuestion> questions)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (questions.Count == 0) return content;
        var sb = new StringBuilder(content.Body.TrimEnd()).AppendLine().AppendLine().AppendLine(Heading);
        foreach (var question in questions)
        {
            var specs = question.SpecIds.Count == 0 ? string.Empty : $" ({string.Join(", ", question.SpecIds)})";
            sb.AppendLine($"- {question.Question.Trim()}{specs} — decided: {question.Proposal.Trim()}");
        }
        return content with { Body = sb.ToString() };
    }
}
