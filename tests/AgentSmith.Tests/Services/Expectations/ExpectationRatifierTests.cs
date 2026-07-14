using AgentSmith.Application.Services.Expectations;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Expectations;
using FluentAssertions;

namespace AgentSmith.Tests.Services.Expectations;

/// <summary>p0328: edits are parsed back into the schema, never appended as
/// free prose; verbatim/edited/rejected outcomes carry the edit distance.</summary>
public sealed class ExpectationRatifierTests
{
    private readonly ExpectationRatifier _ratifier = new(new ExpectationDraftValidator());

    private static readonly ExpectationDraft Draft = new(
        "The endpoint returns 500 on empty payloads.",
        ["The endpoint returns 400 on empty payloads.", "Existing callers stay unaffected."],
        ["No new dependencies."],
        null);

    [Fact]
    public void RatifyAnswer_EditedText_ParsedBackIntoSchema()
    {
        var edited = """
            ## Observed
            The endpoint returns 500 on empty payloads.

            ## Expected
            - [ ] The endpoint returns 422 on empty payloads.
            - [ ] Existing callers stay unaffected.
            - [ ] The error body names the missing field.

            ## Constraints
            - No new dependencies.
            """;

        var result = _ratifier.Ratify(Draft, Answer(edited));

        result.Error.Should().BeNull();
        result.IsRejected.Should().BeFalse();
        var expectation = result.Expectation!;
        expectation.Outcome.Should().Be(ExpectationOutcomes.Edited);
        expectation.Draft.Expected.Should().HaveCount(3);
        expectation.Draft.Expected[0].Should().Be("The endpoint returns 422 on empty payloads.");
        expectation.Draft.Constraints.Should().ContainSingle().Which.Should().Be("No new dependencies.");
        expectation.EditDistance.Should().BeGreaterThan(0);
        expectation.RatifiedBy.Should().Be("@operator");
    }

    [Fact]
    public void RatifyAnswer_ApproveVerbatim_ZeroEditDistance()
    {
        var result = _ratifier.Ratify(Draft, Answer("approve"));

        result.Expectation!.Outcome.Should().Be(ExpectationOutcomes.Verbatim);
        result.Expectation.Draft.Should().Be(Draft);
        result.Expectation.EditDistance.Should().Be(0);
    }

    [Fact]
    public void RatifyAnswer_Reject_ReportsRejectedWithRecord()
    {
        var result = _ratifier.Ratify(Draft, Answer("reject", comment: "wrong scope"));

        result.IsRejected.Should().BeTrue();
        result.RejectComment.Should().Be("wrong scope");
        result.Expectation!.Outcome.Should().Be(ExpectationOutcomes.Rejected);
    }

    [Fact]
    public void RatifyAnswer_SystemTimeoutDefault_StampsUnratified()
    {
        var answer = new DialogAnswer("q1", "approve", "timeout", DateTimeOffset.UtcNow, "system");

        var result = _ratifier.Ratify(Draft, answer);

        result.Expectation!.Outcome.Should().Be(ExpectationOutcomes.Unratified);
    }

    [Fact]
    public void RatifyAnswer_EditOverCaps_FailsWithValidationMessage()
    {
        var overflow = "## Expected\n" + string.Join("\n",
            Enumerable.Range(1, ExpectationDraft.MaxExpected + 1).Select(i => $"- Assertion {i}."));

        var result = _ratifier.Ratify(Draft, Answer(overflow));

        result.Error.Should().Contain("schema caps");
    }

    [Fact]
    public void RatifyAnswer_UnparseableText_FailsWithGuidance()
    {
        var result = _ratifier.Ratify(Draft, Answer("looks wrong somehow, dunno"));

        result.Error.Should().Contain("neither approve/reject nor a parseable");
    }

    private static DialogAnswer Answer(string text, string? comment = null) =>
        new("q1", text, comment, DateTimeOffset.UtcNow, "@operator");
}
