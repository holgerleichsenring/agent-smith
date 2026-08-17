using System.Text.Json;
using AgentSmith.Contracts.Events;
using FluentAssertions;

namespace AgentSmith.Tests.Events;

/// <summary>
/// p0423: the five numbers are a CONTRACT, not a convention. A completion event that
/// answers fewer questions than its siblings is the reason a defect costs a run instead
/// of a query, so the rule is enforced over the assembly rather than remembered.
/// </summary>
public sealed class WorkMeasureTests
{
    private static readonly string[] Aggregates =
        [nameof(StepFinishedEvent), nameof(RunFinishedEvent), nameof(PollCycleFinishedEvent)];

    /// <summary>
    /// Every event whose name says a unit of work FINISHED must expose the measure.
    /// Started-events are excluded: they have no duration and no outcome yet.
    /// </summary>
    [Fact]
    public void EveryRecordedUnit_CarriesTheSameFiveMeasures()
    {
        // p0423 decision: a run, a phase and a step are AGGREGATES — their sizes and
        // attempts are sums over the calls inside them, so they are derived (step 3),
        // never a second stored copy that can drift from its parts. A poll cycle is not
        // a unit of a run at all.
        var completions = typeof(RunEvent).Assembly.GetTypes()
            .Where(t => t.IsSealed && !t.IsAbstract && typeof(RunEvent).IsAssignableFrom(t))
            .Where(t => t.Name.Contains("Finished") || t.Name.Contains("Result"))
            .ToList();

        completions.Should().NotBeEmpty("the scan must reach the event assembly");
        var missing = completions
            .Where(t => !typeof(IMeasuredWork).IsAssignableFrom(t))
            .Select(t => t.Name)
            .Where(n => !Aggregates.Contains(n))
            .ToList();

        missing.Should().BeEmpty(
            "a completion event that reports fewer numbers than its siblings is the "
            + "blind spot the next defect hides in");
    }

    [Fact]
    public void ToolResult_ReportsWhatWasProduced_AndWhatReachedTheModel()
    {
        var evt = new ToolResultEvent(
            "run", "read_file", Ok: true, ResultLength: 100_048,
            DateTimeOffset.UtcNow, ArgsLength: 42, UnboundedResultLength: 4_135_613,
            DurationMs: 1_200, Attempt: 2);

        evt.Measure.InputChars.Should().Be(42);
        evt.Measure.OutputChars.Should().Be(4_135_613);
        evt.Measure.DeliveredChars.Should().Be(100_048);
        evt.Measure.DroppedChars.Should().Be(4_035_565);
        evt.Measure.Outcome.Should().Be(WorkOutcome.Ok);
        evt.Measure.Attempt.Should().Be(2);
    }

    [Fact]
    public void AnUnboundedResult_ReportsNothingDropped()
    {
        var evt = new ToolResultEvent(
            "run", "read_file", Ok: false, ResultLength: 80, DateTimeOffset.UtcNow);

        evt.Measure.OutputChars.Should().Be(80, "nothing was cut, so produced == delivered");
        evt.Measure.DroppedChars.Should().Be(0);
        evt.Measure.Outcome.Should().Be(WorkOutcome.Failed);
    }

    [Fact]
    public void SandboxResult_ReadsItsOutcomeFromTheExitCode()
    {
        var green = new SandboxResultEvent("run", "repo", "dotnet build", 0, 78_000, DateTimeOffset.UtcNow);
        var red = new SandboxResultEvent("run", "repo", "dotnet build", 1, 78_000, DateTimeOffset.UtcNow);

        green.Measure.Outcome.Should().Be(WorkOutcome.Ok);
        red.Measure.Outcome.Should().Be(WorkOutcome.Failed);
    }

    /// <summary>
    /// The measure is a VIEW. Serialising it into the persisted payload would store a
    /// second copy of numbers that already have a home, and a reader would then have two
    /// answers to the same question.
    /// </summary>
    [Fact]
    public void TheMeasure_IsNeverPersisted_ItIsComposedFromTheFieldsThatAre()
    {
        var evt = new LlmCallFinishedEvent(
            "run", "sonnet", "agentic-executor", 100, 20, 0.01m, 94_000,
            DateTimeOffset.UtcNow, PromptChars: 356_632, ResponseChars: 0);

        var json = JsonSerializer.Serialize(evt);

        json.Should().NotContain("Measure").And.NotContain("measure");
        json.Should().Contain("356632", "the number itself is stored, once");
    }

    [Fact]
    public void ALlmCallThatFailed_StillReportsHowItEnded()
    {
        var evt = new LlmCallFinishedEvent(
            "run", "sonnet", "agentic-executor", 0, 0, 0m, 1_407_000,
            DateTimeOffset.UtcNow, PromptChars: 356_632, Outcome: WorkOutcome.Cancelled);

        evt.Measure.Outcome.Should().Be(WorkOutcome.Cancelled);
        evt.Measure.DurationMs.Should().Be(1_407_000);
    }
}
