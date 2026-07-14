using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Expectations;

namespace AgentSmith.Application.Services.Expectations;

/// <summary>
/// p0328: turns a DialogAnswer into a ratification outcome. Approve-verbatim /
/// reject ride the answer keyword; anything else — or an approve whose comment
/// carries an edited block — is parsed back into the schema (never appended as
/// free prose) and re-validated against the caps: they apply to human edits
/// exactly as to the model. A timeout's system default ratifies but stamps
/// 'unratified' — visible degradation, not silent consent.
/// </summary>
public sealed class ExpectationRatifier(ExpectationDraftValidator validator)
{
    private static readonly string[] ApproveAnswers = ["y", "yes", "approve", "approved", "ok", "ratify"];
    private static readonly string[] RejectAnswers = ["n", "no", "reject", "rejected"];

    public ExpectationRatificationResult Ratify(ExpectationDraft draft, DialogAnswer answer)
    {
        var keyword = answer.Answer.Trim().ToLowerInvariant();
        if (RejectAnswers.Contains(keyword))
            return ExpectationRatificationResult.Rejected(
                Record(draft, draft, ExpectationOutcomes.Rejected, answer), answer.Comment);
        if (ApproveAnswers.Contains(keyword))
            return RatifyApproved(draft, answer);
        return RatifyEdited(draft, answer.Answer, answer);
    }

    // An approve whose comment parses as an expectation block is an edit riding
    // the comment channel (the Slack/Teams approval cards have no edit button);
    // a comment that is NOT a block is a note — approve verbatim.
    private ExpectationRatificationResult RatifyApproved(ExpectationDraft draft, DialogAnswer answer)
    {
        if (!string.IsNullOrWhiteSpace(answer.Comment)
            && ExpectationEditParser.TryParse(answer.Comment) is { } edited)
            return Validate(draft, edited, answer);
        var outcome = answer.AnsweredBy == "system"
            ? ExpectationOutcomes.Unratified
            : ExpectationOutcomes.Verbatim;
        return ExpectationRatificationResult.Ratified(Record(draft, draft, outcome, answer));
    }

    private ExpectationRatificationResult RatifyEdited(
        ExpectationDraft draft, string text, DialogAnswer answer)
    {
        var edited = ExpectationEditParser.TryParse(text);
        if (edited is null)
            return ExpectationRatificationResult.Unparseable(
                "the answer is neither approve/reject nor a parseable edited expectation block "
                + "(canonical markdown sections or JSON)");
        return Validate(draft, edited, answer);
    }

    private ExpectationRatificationResult Validate(
        ExpectationDraft original, ExpectationDraft edited, DialogAnswer answer)
    {
        // An edit may drop the Observed section — it restates the draft's.
        var normalized = string.IsNullOrWhiteSpace(edited.Observed)
            ? edited with { Observed = original.Observed }
            : edited;
        var errors = validator.Validate(normalized);
        if (errors.Count > 0)
            return ExpectationRatificationResult.Unparseable(
                $"the edited expectation violates the schema caps:\n{string.Join("\n", errors)}");
        return ExpectationRatificationResult.Ratified(
            Record(original, normalized, ExpectationOutcomes.Edited, answer));
    }

    private static RatifiedExpectation Record(
        ExpectationDraft original, ExpectationDraft ratified, string outcome, DialogAnswer answer) =>
        new(ratified, outcome, answer.AnsweredBy, answer.AnsweredAt,
            outcome == ExpectationOutcomes.Edited
                ? ExpectationEditDistance.Between(
                    ExpectationMarkdown.Render(original), ExpectationMarkdown.Render(ratified))
                : 0);
}
