using AgentSmith.Application.Services.Loop;
using FluentAssertions;

namespace AgentSmith.Tests.Loop;

/// <summary>
/// p0422: run 20 died at "Prompt is too long" on a 4,135,613-character prompt, three
/// calls after a 152k one — not growth, an impact. One tool returned megabytes and the
/// failure surfaced at the NEXT model call, far from its cause. Every tool bounded
/// itself, or did not; nothing bounded them all.
/// </summary>
public sealed class BoundedResultAIFunctionTests
{
    [Fact]
    public void AResultWithinBudget_IsUntouched()
    {
        var text = new string('x', 500);

        BoundedResultAIFunction.Bound(text, budgetChars: 1000).Should().Be(text);
    }

    [Fact]
    public void AnOversizedResult_KeepsTheHeadAndTheTail_AndSaysWhatItDropped()
    {
        var text = "START" + new string('x', 10_000) + "END";

        var bound = BoundedResultAIFunction.Bound(text, budgetChars: 1000);

        bound.Should().StartWith("START", "a listing says what it is at the start");
        bound.Should().EndWith("END", "a build log says how it went at the end");
        bound.Should().Contain("characters dropped from the middle");
        bound.Length.Should().BeLessThan(text.Length);
    }

    [Fact]
    public void TheNoteTellsTheModelHowToAskForTheRest()
    {
        var bound = BoundedResultAIFunction.Bound(new string('y', 5_000), budgetChars: 100);

        bound.Should().Contain("narrow the path",
            "a truncation the model cannot act on is just a mystery");
    }
}
