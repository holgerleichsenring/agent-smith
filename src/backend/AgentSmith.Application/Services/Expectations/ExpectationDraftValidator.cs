using AgentSmith.Contracts.Expectations;

namespace AgentSmith.Application.Services.Expectations;

/// <summary>
/// p0328: enforces the ExpectationDraft schema caps (≤5 expected assertions,
/// each one sentence; ≤3 constraints; ≤1 open question with concrete A/B).
/// The error list is fed back to the model verbatim on a drafting retry, and
/// applies unchanged to operator edits — the caps ARE the contract.
/// </summary>
public sealed class ExpectationDraftValidator
{
    public IReadOnlyList<string> Validate(ExpectationDraft draft)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(draft.Observed))
            errors.Add("'observed' must not be empty.");
        ValidateExpected(draft, errors);
        if (draft.Constraints.Count > ExpectationDraft.MaxConstraints)
            errors.Add($"'constraints' has {draft.Constraints.Count} entries — the cap is "
                + $"{ExpectationDraft.MaxConstraints}. Keep the binding boundaries only.");
        ValidateOpenQuestion(draft.OpenQuestion, errors);
        return errors;
    }

    private static void ValidateExpected(ExpectationDraft draft, List<string> errors)
    {
        if (draft.Expected.Count == 0)
            errors.Add("'expected' must contain at least one testable assertion.");
        if (draft.Expected.Count > ExpectationDraft.MaxExpected)
            errors.Add($"'expected' has {draft.Expected.Count} assertions — the cap is "
                + $"{ExpectationDraft.MaxExpected}. What does not fit is a design document; "
                + "state the most valuable assertions instead of overflowing.");
        foreach (var assertion in draft.Expected.Where(a =>
                     a.Trim().Length > ExpectationDraft.MaxAssertionLength))
            errors.Add($"Assertion exceeds {ExpectationDraft.MaxAssertionLength} characters — "
                + $"one testable sentence, not prose: \"{Truncate(assertion)}\"");
    }

    private static void ValidateOpenQuestion(ExpectationOpenQuestion? question, List<string> errors)
    {
        if (question is null) return;
        if (string.IsNullOrWhiteSpace(question.Question)
            || string.IsNullOrWhiteSpace(question.OptionA)
            || string.IsNullOrWhiteSpace(question.OptionB))
            errors.Add("'open_question' must carry a question AND two concrete options A and B "
                + "— an open-ended question pushes authoring cost back onto the human.");
    }

    private static string Truncate(string text) =>
        text.Length <= 60 ? text : text[..60] + "…";
}
