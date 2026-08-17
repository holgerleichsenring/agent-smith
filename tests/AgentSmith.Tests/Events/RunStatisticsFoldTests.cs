using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Runs;
using FluentAssertions;

namespace AgentSmith.Tests.Events;

/// <summary>
/// p0423b: the ticket's statistics — phases, duration per phase and per ticket, calls and
/// their sizes — are a fold over the recorded trail, grouped by the phase each step
/// belonged to. No counter is kept while the run happens, so nothing can drift.
/// </summary>
public sealed class RunStatisticsFoldTests
{
    private static readonly RunStepFacts[] TwoPhases =
    [
        new(1, "p1", 60_000),
        new(2, "p1", 30_000),
        new(3, "p2", 120_000),
    ];

    [Fact]
    public void TicketStatistics_ComeFromTheTrail_NotFromCounters()
    {
        var view = RunStatisticsFold.Fold(Trail(), TwoPhases);

        view.Totals.Calls.Should().Be(3);
        view.TotalDurationMs.Should().Be(210_000, "the ticket lasts as long as its phases did");
        view.Phases.Should().HaveCount(2);
        view.Phases[0].PhaseId.Should().Be("p1");
        view.Phases[0].Steps.Should().Be(2);
        view.Phases[0].DurationMs.Should().Be(90_000);
        view.Phases[0].Calls.Calls.Should().Be(2);
        view.Phases[1].PhaseId.Should().Be("p2");
        view.Phases[1].Calls.Calls.Should().Be(1);
    }

    [Fact]
    public void APhase_ReportsTheCommandsThatCameBackNonZero()
    {
        var view = RunStatisticsFold.Fold(Trail(), TwoPhases);

        view.Phases[0].Commands.Should().Be(1);
        view.Phases[0].FailedCommands.Should().Be(0);
        view.Phases[1].Commands.Should().Be(1);
        view.Phases[1].FailedCommands.Should().Be(1, "exit code 1 is the evidence, not the summary");
        view.Commands.Select(c => c.ExitCode).Should().Equal(0, 1);
    }

    /// <summary>
    /// The shape that named the wall in run 26 must survive the fold as a SERIES: the
    /// prompt grows while the answer shrinks, in call order, attributed to its phase.
    /// </summary>
    [Fact]
    public void TheCallSeries_KeepsCallOrder_AndCarriesBothSizes()
    {
        var view = RunStatisticsFold.Fold(Trail(), TwoPhases);

        view.Calls.Select(c => c.Index).Should().Equal(1, 2, 3);
        view.Calls.Select(c => c.PromptChars).Should().Equal(151_040, 216_004, 356_632);
        view.Calls.Select(c => c.AnswerChars).Should().Equal(3_886, 969, 0);
        view.Calls.Select(c => c.PhaseId).Should().Equal("p1", "p1", "p2");
        view.Calls[2].Outcome.Should().Be(nameof(WorkOutcome.Cancelled));
        view.Truncated.Should().BeFalse();
    }

    [Fact]
    public void AnOverLongSeries_KeepsItsTail_AndSaysSo()
    {
        var events = Enumerable.Range(0, 5)
            .Select(i => (RunEvent)Call(1, 1_000 + i, 1, 1))
            .ToList();

        var view = RunStatisticsFold.Fold(events, [new RunStepFacts(1, "p1", 0)], maxPoints: 2);

        view.Truncated.Should().BeTrue();
        // The end of a run is where the shape gets interesting, so the tail is what survives.
        view.Calls.Select(c => c.Index).Should().Equal(4, 5);
    }

    [Fact]
    public void AStepThatBelongsToNoPhase_IsItsOwnGroup_NotDropped()
    {
        var view = RunStatisticsFold.Fold(
            [Call(1, 10, 10, 1)], [new RunStepFacts(1, null, 5_000)]);

        view.Phases.Should().HaveCount(1);
        view.Phases[0].PhaseId.Should().BeNull();
        view.Phases[0].Calls.Calls.Should().Be(1);
    }

    private static List<RunEvent> Trail() =>
    [
        Call(1, 151_040, 3_886, 9_300),
        new SandboxResultEvent("run", "repo", "dotnet build", 0, 4_000, DateTimeOffset.UtcNow)
            { OriginStepIndex = 1 },
        Call(2, 216_004, 969, 94_000),
        Call(3, 356_632, 0, 1_407_000, WorkOutcome.Cancelled),
        new SandboxResultEvent("run", "repo", "dotnet test", 1, 9_000, DateTimeOffset.UtcNow)
            { OriginStepIndex = 3 },
    ];

    private static LlmCallFinishedEvent Call(
        int stepIndex, long promptChars, long responseChars, long durationMs,
        WorkOutcome outcome = WorkOutcome.Ok) =>
        new("run", "sonnet", "agentic-executor", 0, 0, 0m, durationMs, DateTimeOffset.UtcNow,
            PromptChars: promptChars, ResponseChars: responseChars, Outcome: outcome)
        { OriginStepIndex = stepIndex };
}
