using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Services.Events;
using FluentAssertions;

namespace AgentSmith.Tests.Server;

/// <summary>
/// p0344b: the run-story beat states are derived SERVER-side from the run's
/// typed command progress (RunStep.CommandName), never from display labels. A
/// run whose stored steps predate the typed command name yields beats=null —
/// the client renders no storybar instead of guessing.
/// </summary>
public sealed class RunBeatsComputerTests
{
    // p0344b spec test: Beats_TypedCommands_MapDeterministically_PerPipeline
    // (derivation half — a finished fix-bug run renders its beats from its
    //  actual typed steps: everything done, the preset's absent verify beat
    //  skipped, never guessed from labels).
    [Fact]
    public void FinishedFixBugRun_AllStepsOk_TicketPlanBuildingOutcomeDone_VerifySkipped()
    {
        var run = TerminalRun("fix-bug", "success", Steps(
            ("s", CommandNames.LoadCatalog), ("s", CommandNames.FetchTicket),
            ("s", CommandNames.CheckoutSource), ("s", CommandNames.AnalyzeCode),
            ("s", CommandNames.GeneratePlan), ("s", CommandNames.Approval),
            ("s", CommandNames.AgenticMaster), ("s", CommandNames.WriteRunResult),
            ("s", CommandNames.CommitAndPR)));

        var beats = RunBeatsComputer.Compute(run)!;

        beats.Ticket.Should().Be(BeatStates.Done);
        beats.Plan.Should().Be(BeatStates.Done);
        beats.Building.Should().Be(BeatStates.Done);
        beats.Verify.Should().Be(BeatStates.Skipped, "the fix-bug preset has no verify-beat command");
        beats.Outcome.Should().Be(BeatStates.Done);
    }

    // p0344b spec test: Beats_FailedStep_MarksItsBeatFailed_LaterBeatsPending
    [Fact]
    public void Beats_FailedStep_MarksItsBeatFailed_LaterBeatsPending()
    {
        var run = TerminalRun("fix-bug", "failed", Steps(
            ("s", CommandNames.LoadCatalog), ("s", CommandNames.FetchTicket),
            ("s", CommandNames.CheckoutSource), ("s", CommandNames.AnalyzeCode),
            ("f", CommandNames.GeneratePlan)));

        var beats = RunBeatsComputer.Compute(run)!;

        beats.Ticket.Should().Be(BeatStates.Done);
        beats.Plan.Should().Be(BeatStates.Failed, "the failed step's beat is the failure point");
        beats.Building.Should().Be(BeatStates.Done, "AnalyzeCode completed before the plan failed — honest, not narrative-smoothed");
        beats.Verify.Should().Be(BeatStates.Skipped, "the preset never runs a verify-beat command");
        beats.Outcome.Should().Be(BeatStates.Pending, "the story stopped before shipping");
    }

    [Fact]
    public void ActiveRun_CurrentBeatActive_EarlierDone_LaterPending()
    {
        var run = ActiveRun("fix-bug", Steps(
            ("s", CommandNames.LoadCatalog), ("s", CommandNames.FetchTicket),
            ("s", CommandNames.CheckoutSource), ("s", CommandNames.AnalyzeCode),
            ("s", CommandNames.GeneratePlan), ("s", CommandNames.Approval),
            ("r", CommandNames.AgenticMaster)));

        var beats = RunBeatsComputer.Compute(run)!;

        beats.Ticket.Should().Be(BeatStates.Done);
        beats.Plan.Should().Be(BeatStates.Done);
        beats.Building.Should().Be(BeatStates.Active, "the run is inside the master step");
        beats.Verify.Should().Be(BeatStates.Skipped);
        beats.Outcome.Should().Be(BeatStates.Pending);
    }

    // p0344b spec test: RunDetail_PreMigrationRow_BeatsNull_ClientRendersNoStorybar
    // (server half — pre-migration steps carry no CommandName, so no mapping exists).
    [Fact]
    public void PreMigrationRow_StepsWithoutCommandName_BeatsNull()
    {
        var run = TerminalRun("fix-bug", "success",
        [
            new RunStep { Id = 1, StepIndex = 0, StepName = "Fetching ticket", Status = "success" },
            new RunStep { Id = 2, StepIndex = 1, StepName = "Executing master", Status = "success" },
        ]);

        RunBeatsComputer.Compute(run).Should().BeNull(
            "pre-p0344b rows cannot be mapped and must not be guessed from labels");
    }

    [Fact]
    public void QueuedRun_NoStepsYet_PlannedBeatsPending()
    {
        var run = new Run { Id = "r1", Pipeline = "fix-bug", Status = "queued" };

        var beats = RunBeatsComputer.Compute(run)!;

        beats.Ticket.Should().Be(BeatStates.Pending);
        beats.Plan.Should().Be(BeatStates.Pending);
        beats.Building.Should().Be(BeatStates.Pending);
        beats.Verify.Should().Be(BeatStates.Skipped, "the preset contains no verify-beat command");
        beats.Outcome.Should().Be(BeatStates.Pending);
    }

    [Fact]
    public void QueuedRun_UnknownPipeline_NoStepsYet_BeatsNull()
    {
        var run = new Run { Id = "r1", Pipeline = "operator-custom", Status = "queued" };

        RunBeatsComputer.Compute(run).Should().BeNull("an unknown pipeline promises no beats");
    }

    [Fact]
    public void CancelledRun_DiesMidStep_ThatBeatFailed()
    {
        var run = TerminalRun("fix-bug", "cancelled", Steps(
            ("s", CommandNames.LoadCatalog), ("s", CommandNames.FetchTicket),
            ("r", CommandNames.AgenticMaster)));

        var beats = RunBeatsComputer.Compute(run)!;

        beats.Ticket.Should().Be(BeatStates.Done);
        beats.Building.Should().Be(BeatStates.Failed, "the run died inside this beat");
        beats.Outcome.Should().Be(BeatStates.Pending);
    }

    [Fact]
    public void ParameterisedRoundCommands_MapThroughBaseCommand()
    {
        var run = ActiveRun("mad-discussion", Steps(
            ("s", CommandNames.LoadCatalog), ("s", CommandNames.LoadSkills),
            ("r", $"{CommandNames.SkillRound}:architect:1")));

        var beats = RunBeatsComputer.Compute(run)!;

        beats.Building.Should().Be(BeatStates.Active, "SkillRoundCommand:architect:1 is a building step");
    }

    private static Run TerminalRun(string pipeline, string status, ICollection<RunStep> steps) => new()
    {
        Id = "r1", Pipeline = pipeline, Status = status,
        StartedAt = DateTimeOffset.Parse("2026-07-16T10:00:00Z"),
        FinishedAt = DateTimeOffset.Parse("2026-07-16T10:10:00Z"),
        Steps = steps,
    };

    private static Run ActiveRun(string pipeline, ICollection<RunStep> steps) => new()
    {
        Id = "r1", Pipeline = pipeline, Status = "running",
        StartedAt = DateTimeOffset.Parse("2026-07-16T10:00:00Z"),
        Steps = steps,
    };

    private static List<RunStep> Steps(params (string State, string Command)[] steps) =>
        steps.Select((s, i) => new RunStep
        {
            Id = i + 1,
            StepIndex = i,
            StepName = s.Command,
            CommandName = s.Command,
            Status = s.State switch { "s" => "success", "f" => "failed", _ => "running" },
        }).ToList();
}
