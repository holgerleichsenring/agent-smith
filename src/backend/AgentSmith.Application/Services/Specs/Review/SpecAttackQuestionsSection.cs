using System.Text;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-11-11-2bdfd: the operator's open questions of a design turn, each with the design partner's proposal,
/// rendered after the siblings and before the draft. The reviewer judges the draft as if each proposal is taken.
/// 2026-11-13-b3a2: a decision only the owner can make goes into a finding's <c>decision</c> field as a question; that
/// rule renders whether or not open questions exist, the questions themselves only when there are some.
/// </summary>
public static class SpecAttackQuestionsSection
{
    public const string DecisionHeading = "## Decisions only the owner can make";

    public const string DecisionRule =
        "A policy, value or trade-off the material leaves open or marks unapproved is the owner's decision, not a "
        + "defect of the spec: name it in the finding's \"decision\" field, worded as a question, and cite the "
        + "material's line you read (under reference:<name> for an uploaded file) like any finding. When every "
        + "objection you have is such a decision, answer REFUSE with every finding carrying its decision; the draft "
        + "then waits on the owner instead of being reworked.";

    public const string Heading = "## Questions the operator still answers";

    public const string Rule =
        "Judge the spec as if each proposal below is taken. A finding that only restates one of these questions is "
        + "not a finding. A decision the material leaves to the owner that no question here covers IS a finding, "
        + "with its \"decision\" field set.";

    public static string Render(IReadOnlyList<OpenQuestion> questions)
    {
        ArgumentNullException.ThrowIfNull(questions);
        var sb = new StringBuilder().AppendLine().AppendLine(DecisionHeading).AppendLine(DecisionRule);
        if (questions.Count == 0) return sb.ToString();
        sb.AppendLine().AppendLine(Heading).AppendLine(Rule).AppendLine();
        foreach (var question in questions)
        {
            sb.AppendLine($"- {question.Question.Trim()}").AppendLine($"  proposed: {question.Proposal.Trim()}");
            if (question.SpecIds.Count > 0) sb.AppendLine($"  specs: {string.Join(", ", question.SpecIds)}");
        }
        return sb.ToString();
    }
}
