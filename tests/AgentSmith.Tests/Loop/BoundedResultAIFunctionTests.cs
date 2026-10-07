using AgentSmith.Application.Services.Loop;
using FluentAssertions;
using Microsoft.Extensions.AI;

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
    public async Task AResultWithinBudget_IsUntouched()
    {
        var text = new string('x', 500);

        var result = await Bounded(text, budgetChars: 1000).InvokeAsync(new AIFunctionArguments());

        result.Should().Be(text);
    }

    [Fact]
    public async Task AnOversizedResult_KeepsTheHeadAndTheTail_AndSaysWhatItDropped()
    {
        var text = "START" + new string('x', 10_000) + "END";

        var bound = (string)(await Bounded(text, budgetChars: 1000).InvokeAsync(new AIFunctionArguments()))!;

        bound.Should().StartWith("START", "a listing says what it is at the start");
        bound.Should().EndWith("END", "a build log says how it went at the end");
        bound.Should().Contain("characters cut from the middle").And.Contain("10,008");
        bound.Length.Should().BeLessThanOrEqualTo(1000);
    }

    [Fact]
    public async Task TheNoteTellsTheModelHowToAskForTheRest()
    {
        var bound = (string)(await Bounded(new string('y', 5_000), budgetChars: 1000)
            .InvokeAsync(new AIFunctionArguments()))!;

        bound.Should().Contain("start_line/line_count").And.Contain("narrower path or pattern",
            "a truncation the model cannot act on is just a mystery");
    }

    [Fact]
    public async Task BoundedResultAIFunction_FactoryToolReturning200k_IsBounded()
    {
        var reporter = new ResultBoundReporter();
        var tool = new BoundedResultAIFunction(
            AIFunctionFactory.Create(() => new string('z', 200_000), "read_file"), reporter);

        using var scope = reporter.Begin();
        var result = await tool.InvokeAsync(new AIFunctionArguments());

        result.Should().BeOfType<string>("a factory tool's JSON-string result is bounded, not passed through");
        ((string)result!).Length.Should().BeLessThanOrEqualTo(100_000);
        ((string)result!).Should().Contain("200,000");
        scope.OriginalChars.Should().Be(200_000);
    }

    private static BoundedResultAIFunction Bounded(string text, int budgetChars) =>
        new(AIFunctionFactory.Create(() => text, "tool"), budgetChars: budgetChars);
}
