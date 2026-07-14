using AgentSmith.Application.Services.Expectations;
using AgentSmith.Contracts.Expectations;
using FluentAssertions;

namespace AgentSmith.Tests.Services.Expectations;

/// <summary>p0328: the schema caps are enforced in the type's validator —
/// overflow rejects with a message the model (or operator) can act on.</summary>
public sealed class ExpectationDraftValidatorTests
{
    private readonly ExpectationDraftValidator _validator = new();

    [Fact]
    public void ExpectationDraft_OverCaps_ValidationRejects()
    {
        var draft = new ExpectationDraft(
            "Observed something.",
            Expected: Enumerable.Range(1, ExpectationDraft.MaxExpected + 1)
                .Select(i => $"Assertion {i} holds.").ToList(),
            Constraints: Enumerable.Range(1, ExpectationDraft.MaxConstraints + 1)
                .Select(i => $"Constraint {i}.").ToList(),
            OpenQuestion: new ExpectationOpenQuestion("Which way?", "", ""));

        var errors = _validator.Validate(draft);

        errors.Should().HaveCount(3);
        errors.Should().ContainMatch("*'expected' has 6 assertions*cap is 5*");
        errors.Should().ContainMatch("*'constraints' has 4 entries*cap is 3*");
        errors.Should().ContainMatch("*two concrete options A and B*");
    }

    [Fact]
    public void ExpectationDraft_WithinCaps_ValidationPasses()
    {
        var draft = new ExpectationDraft(
            "The endpoint returns 500 on empty payloads.",
            ["The endpoint returns 400 on empty payloads.", "Existing callers stay unaffected."],
            ["No new dependencies."],
            new ExpectationOpenQuestion("Log level?", "warning", "error"));

        _validator.Validate(draft).Should().BeEmpty();
    }

    [Fact]
    public void ExpectationDraft_EmptyExpected_ValidationRejects()
    {
        var draft = new ExpectationDraft("Observed.", [], [], null);

        _validator.Validate(draft).Should().ContainMatch("*at least one testable assertion*");
    }
}
