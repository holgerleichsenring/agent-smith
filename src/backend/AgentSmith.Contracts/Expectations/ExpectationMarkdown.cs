using System.Text;

namespace AgentSmith.Contracts.Expectations;

/// <summary>
/// p0328: THE canonical markdown form of an <see cref="ExpectationDraft"/> —
/// one renderer for every surface (dialogue question, ticket comment, prompt
/// section, PR body, result.md) so an operator's edited reply can be parsed
/// back into the schema by <c>ExpectationEditParser</c> from any of them.
/// Pure mapping, no I/O.
/// </summary>
public static class ExpectationMarkdown
{
    public const string ObservedHeading = "## Observed";
    public const string ExpectedHeading = "## Expected";
    public const string ConstraintsHeading = "## Constraints";
    public const string OpenQuestionHeading = "## Open question";

    public static string Render(ExpectationDraft draft, bool checkboxes = false)
    {
        var sb = new StringBuilder();
        sb.AppendLine(ObservedHeading);
        sb.AppendLine(draft.Observed.Trim());
        sb.AppendLine();
        sb.AppendLine(ExpectedHeading);
        foreach (var assertion in draft.Expected)
            sb.AppendLine(checkboxes ? $"- [ ] {assertion.Trim()}" : $"- {assertion.Trim()}");
        AppendConstraints(sb, draft.Constraints);
        AppendOpenQuestion(sb, draft.OpenQuestion);
        return sb.ToString().TrimEnd();
    }

    private static void AppendConstraints(StringBuilder sb, IReadOnlyList<string> constraints)
    {
        if (constraints.Count == 0) return;
        sb.AppendLine();
        sb.AppendLine(ConstraintsHeading);
        foreach (var constraint in constraints)
            sb.AppendLine($"- {constraint.Trim()}");
    }

    private static void AppendOpenQuestion(StringBuilder sb, ExpectationOpenQuestion? question)
    {
        if (question is null) return;
        sb.AppendLine();
        sb.AppendLine(OpenQuestionHeading);
        sb.AppendLine(question.Question.Trim());
        sb.AppendLine($"- A: {question.OptionA.Trim()}");
        sb.AppendLine($"- B: {question.OptionB.Trim()}");
    }
}
