using System.Text;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-11-11-2bdfd: the operator's open questions of a design turn, each with the design partner's proposal,
/// rendered after the siblings and before the draft. The reviewer judges the draft as if each proposal is taken, and
/// names a decision only the owner can make as a question rather than demanding a rewrite. Empty with no questions.
/// </summary>
public static class SpecAttackQuestionsSection
{
    public const string Heading = "## Questions the operator still answers";

    public const string Rule =
        "Judge the spec as if each proposal below is taken. A finding that only restates one of these questions is "
        + "not a finding. A decision the material leaves to the owner that no question here covers IS a finding: "
        + "name the question the operator should be asked.";

    public static string Render(IReadOnlyList<OpenQuestion> questions)
    {
        ArgumentNullException.ThrowIfNull(questions);
        if (questions.Count == 0) return string.Empty;
        var sb = new StringBuilder().AppendLine().AppendLine(Heading).AppendLine(Rule).AppendLine();
        foreach (var question in questions)
        {
            sb.AppendLine($"- {question.Question.Trim()}").AppendLine($"  proposed: {question.Proposal.Trim()}");
            if (question.SpecIds.Count > 0) sb.AppendLine($"  specs: {string.Join(", ", question.SpecIds)}");
        }
        return sb.ToString();
    }
}
