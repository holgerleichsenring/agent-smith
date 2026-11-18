using AgentSmith.Contracts.Reviews;
using FluentAssertions;

namespace AgentSmith.Tests.Reviews;

/// <summary>2026-11-17-3435a: the one rule a head's CI is decided by, and the excerpt that keeps a log's end.</summary>
public sealed class CiVerdictTests
{
    private static CiCheck C(CiCheckOutcome outcome, bool required = false) => new("c", outcome, required, null);

    [Fact]
    public void AnyFailed_IsRed_EvenWhileOthersRun() =>
        CiVerdict.Of([C(CiCheckOutcome.Failed), C(CiCheckOutcome.Pending)]).Should().Be(CiStates.Red);

    [Fact]
    public void Running_IsPending_AllPassed_IsGreen_NoneIsNone()
    {
        CiVerdict.Of([C(CiCheckOutcome.Passed), C(CiCheckOutcome.Pending)]).Should().Be(CiStates.Pending);
        CiVerdict.Of([C(CiCheckOutcome.Passed)]).Should().Be(CiStates.Green);
        CiVerdict.Of([]).Should().Be(CiStates.None);
    }

    [Fact]
    public void RequiredChecks_AloneDecide() =>
        CiVerdict.Of([C(CiCheckOutcome.Failed), C(CiCheckOutcome.Passed, required: true)]).Should().Be(CiStates.Green);

    [Fact]
    public void Excerpt_KeepsTheEndOfALongLog_WithinTheBound()
    {
        var excerpt = CiExcerpt.Of(["build failed"], new string('x', 5000) + "THE END", 200);

        excerpt.Length.Should().BeLessThanOrEqualTo(200);
        excerpt.Should().StartWith("build failed").And.Contain(CiExcerpt.Cut).And.EndWith("THE END");
    }
}
