using AgentSmith.Application.Services.Pipeline;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pipeline;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Services;

/// <summary>
/// 2026-09-17-0e79e: the executor's loop guard is the budget the run published, re-read on
/// every pass — and exhausting it is reported the way a failing step is, so the finalizer
/// tail runs and the phases that verified are still delivered.
/// </summary>
public sealed class PipelineExecutorBudgetTests
{
    private const string Loop = "LoopingCommand";

    [Fact]
    public async Task PipelineExecutor_WithinBudget_BehavesExactlyAsBefore()
    {
        var h = new PipelineExecutorTestBuilder();
        var project = new ResolvedProject();
        var pipeline = new PipelineContext();
        var step = Arrange(h, "PlainCommand", project, pipeline, CommandResult.Ok("done"));

        var result = await h.Sut.ExecuteAsync(
            ["PlainCommand"], project, pipeline, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("Pipeline completed");
        h.ExecutorMock.Verify(e => e.ExecuteAsync(step, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PipelineExecutor_InsertionLoop_StopsAtTheBudget()
    {
        var h = new PipelineExecutorTestBuilder();
        var project = new ResolvedProject();
        var pipeline = WithBudget(new StepBudget(5, "a test budget"));
        var looping = ArrangeLoop(h, project, pipeline);

        var result = await h.Sut.ExecuteAsync([Loop], project, pipeline, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        h.ExecutorMock.Verify(e => e.ExecuteAsync(looping, It.IsAny<CancellationToken>()),
            Times.Exactly(5), "the guard is the published number, not the old constant");
    }

    [Fact]
    public async Task PipelineExecutor_BudgetPublishedMidRun_IsTheGuardFromThenOn()
    {
        // PhaseSequence publishes the budget from inside the run, so a budget captured once
        // before the loop would be the default for every run that needs a different number.
        var h = new PipelineExecutorTestBuilder();
        var project = new ResolvedProject();
        var pipeline = new PipelineContext();
        Publisher(h, project, pipeline, new StepBudget(3, "published mid-run"));
        var looping = ArrangeLoop(h, project, pipeline);

        var result = await h.Sut.ExecuteAsync(
            ["Publisher", Loop], project, pipeline, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("published mid-run");
        h.ExecutorMock.Verify(e => e.ExecuteAsync(looping, It.IsAny<CancellationToken>()),
            Times.Exactly(2), "one step was already spent when the budget of 3 was published");
    }

    [Fact]
    public async Task PipelineExecutor_BudgetExhausted_MessageNamesTheBudgetAndItsOrigin()
    {
        var h = new PipelineExecutorTestBuilder();
        var project = new ResolvedProject();
        var pipeline = WithBudget(StepBudget.ForPhases(1));
        ArrangeLoop(h, project, pipeline);

        var result = await h.Sut.ExecuteAsync([Loop], project, pipeline, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should()
            .Contain($"step budget {StepBudget.ForPhases(1).Limit}")
            .And.Contain(StepBudget.ForPhases(1).Origin, "an exhausted run is read without arithmetic")
            .And.Contain("infinite loop in command insertion");
    }

    [Fact]
    public async Task PipelineExecutor_BudgetExhausted_RunsTheFinalizerTail()
    {
        // Before this phase the ceiling returned a bare failed result BEFORE the step ran,
        // so the run wrote no result, opened no pull request and left the ticket unfinalized.
        var h = new PipelineExecutorTestBuilder();
        var project = new ResolvedProject();
        var pipeline = WithBudget(new StepBudget(3, "a test budget"));
        ArrangeLoop(h, project, pipeline);
        var runResult = Arrange(h, CommandNames.WriteRunResult, project, pipeline, CommandResult.Ok("written"));
        var commitAndPr = Arrange(h, CommandNames.CommitAndPR, project, pipeline, CommandResult.Ok("pushed"));

        await h.Sut.ExecuteAsync(
            [Loop, CommandNames.WriteRunResult, CommandNames.CommitAndPR],
            project, pipeline, CancellationToken.None);

        h.ExecutorMock.Verify(e => e.ExecuteAsync(runResult, It.IsAny<CancellationToken>()), Times.Once);
        h.ExecutorMock.Verify(e => e.ExecuteAsync(commitAndPr, It.IsAny<CancellationToken>()), Times.Once);
        pipeline.Get<string>(ContextKeys.FailureReason).Should().Contain("step budget 3");
    }

    [Fact]
    public async Task PipelineExecutor_BudgetExhausted_DeliversTheVerifiedPhasesAsAShortfall()
    {
        var h = new PipelineExecutorTestBuilder();
        var ticket = new Mock<ITicketProvider>();
        h.TicketFactoryMock.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(ticket.Object);
        var project = new ResolvedProject();
        var pipeline = WithBudget(new StepBudget(3, "a test budget"));
        pipeline.Set(ContextKeys.TicketId, new TicketId("42"));
        ArrangeLoop(h, project, pipeline);
        var commitAndPr = Arrange(h, CommandNames.CommitAndPR, project, pipeline, CommandResult.Ok("delivered"));
        h.ExecutorMock.Setup(e => e.ExecuteAsync(commitAndPr, It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                new RunShortfall(
                    [new PhaseProgress("p0001a", "a", PhaseRunState.Done)],
                    [new PhaseProgress("p0001b", "b", PhaseRunState.NotStarted)],
                    "step budget exhausted").MarkDelivered(pipeline);
                return Task.FromResult(CommandResult.Ok("delivered"));
            });

        var result = await h.Sut.ExecuteAsync(
            [Loop, CommandNames.CommitAndPR], project, pipeline, CancellationToken.None);

        result.IsSuccess.Should().BeTrue("the verified phases reached the pull request");
        result.Message.Should().Contain("Delivered 1 of 2 phase(s)").And.Contain("p0001b");
        h.LifecycleMock.Verify(l => l.MarkFailed(), Times.Never);
        ticket.Verify(t => t.FinalizeAsync(
            It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never, "the delivery finalized the ticket in the tail; the error path never runs");
    }

    [Fact]
    public async Task PipelineExecutor_BudgetExhaustedWithoutAShortfall_ReportsTheFailureLikeAnyStep()
    {
        var h = new PipelineExecutorTestBuilder();
        var ticket = new Mock<ITicketProvider>();
        h.TicketFactoryMock.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(ticket.Object);
        var project = new ResolvedProject();
        var pipeline = WithBudget(new StepBudget(2, "a test budget"));
        pipeline.Set(ContextKeys.TicketId, new TicketId("42"));
        ArrangeLoop(h, project, pipeline);

        var result = await h.Sut.ExecuteAsync([Loop], project, pipeline, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        h.LifecycleMock.Verify(l => l.MarkFailed(), Times.Once);
        ticket.Verify(t => t.FinalizeAsync(
            It.Is<TicketId>(id => id.Value == "42"),
            It.Is<string>(s => s.Contains("<b>Agent Smith — Failed</b>")),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()),
            Times.Once, "an exhausted run got no failure comment and no terminal status before");
    }

    [Fact]
    public async Task PipelineExecutor_BudgetExhausted_TheCommentNamesTheStepItStoppedAt()
    {
        // A result carrying no StepName renders "<b>Step:</b>  (0/0)" on the ticket, logs
        // "stopped at step 0:  failed" and stamps an empty step into the WIP trailer — the
        // failure report this phase exists to produce, saying nothing about where it stopped.
        var h = new PipelineExecutorTestBuilder();
        var ticket = new Mock<ITicketProvider>();
        h.TicketFactoryMock.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(ticket.Object);
        var project = new ResolvedProject();
        var pipeline = WithBudget(new StepBudget(2, "a test budget"));
        pipeline.Set(ContextKeys.TicketId, new TicketId("42"));
        ArrangeLoop(h, project, pipeline);

        var result = await h.Sut.ExecuteAsync([Loop], project, pipeline, CancellationToken.None);

        var expected = StepLabelComposer.Label(PipelineCommand.Simple(Loop));
        expected.Should().NotBeEmpty("the premise: the stopped-at node has a label");
        result.StepName.Should().Be(expected, "the step the run never got to is named");
        result.FailedStep.Should().Be(3, "numbered the way the runner would have numbered it");
        result.TotalSteps.Should().Be(3, "against the list the insertions grew, not the preset");
        ticket.Verify(t => t.FinalizeAsync(
            It.IsAny<TicketId>(),
            It.Is<string>(s => s.Contains($"<b>Step:</b> {expected} (3/3)")
                               && !s.Contains("(0/0)")),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
        pipeline.Get<string>(ContextKeys.FailedStepName).Should().Be(
            expected, "the WIP commit's trailer reads the same field");
    }

    [Fact]
    public async Task PipelineExecutor_NothingPublished_StopsAtTheHundredItAlwaysHad()
    {
        // The claim every sequence-less preset rests on, proven through the executor rather
        // than by comparing one constant to another.
        var h = new PipelineExecutorTestBuilder();
        var project = new ResolvedProject();
        var pipeline = new PipelineContext();
        pipeline.Has(ContextKeys.StepBudget).Should().BeFalse("the premise: nothing published one");
        var looping = ArrangeLoop(h, project, pipeline);

        var result = await h.Sut.ExecuteAsync([Loop], project, pipeline, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        h.ExecutorMock.Verify(e => e.ExecuteAsync(looping, It.IsAny<CancellationToken>()),
            Times.Exactly(100), "exactly the ceiling the executor had before this phase");
    }

    [Fact]
    public async Task PipelineExecutor_BudgetChangesMidRun_TheChangeIsLogged()
    {
        // The start line is written before any step runs, so on a fresh coding run it names
        // the default for a run that will work to a different number. The change is logged.
        var h = new PipelineExecutorTestBuilder();
        var project = new ResolvedProject();
        var pipeline = new PipelineContext();
        var publisher = Publisher(h, project, pipeline, new StepBudget(3, "8 phases worth"));
        ArrangeLoop(h, project, pipeline);

        await h.Sut.ExecuteAsync(["Publisher", Loop], project, pipeline, CancellationToken.None);

        publisher.Should().NotBeNull();
        h.Log.Lines.Should().Contain(l => l.Contains("step budget 100 (no phase sequence)"),
            "the start line says what the run entered with");
        h.Log.Lines.Should().Contain(l => l.Contains("now works to step budget 3 (8 phases worth)"),
            "and the run says so when the number it works to changes");
        h.Log.Lines.Count(l => l.Contains("now works to")).Should().Be(
            1, "the line is written once per change, not once per pass");
    }

    [Fact]
    public async Task PipelineExecutor_Resume_ReadsTheBudgetFromTheRehydratedContext()
    {
        // A resume rehydrates the context and splices no sequence of its own, so the budget
        // it works to is the one the checkpoint carried — not the default.
        var h = new PipelineExecutorTestBuilder();
        var project = new ResolvedProject();
        var pipeline = WithBudget(new StepBudget(4, "restored from a checkpoint"));
        var looping = ArrangeLoop(h, project, pipeline);

        var result = await h.Sut.ResumeAsync(
            [PipelineCommand.Simple(Loop)], project, pipeline, 0, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("restored from a checkpoint");
        h.ExecutorMock.Verify(e => e.ExecuteAsync(looping, It.IsAny<CancellationToken>()), Times.Exactly(4));
    }

    [Fact]
    public async Task PipelineExecutor_Resume_GetsItsOwnWindow()
    {
        // The check subtracts the segment start, so a resumed segment starts a fresh window.
        // A resume follows a person answering a question — work somebody asked for.
        var h = new PipelineExecutorTestBuilder();
        var project = new ResolvedProject();
        var pipeline = WithBudget(new StepBudget(3, "a test budget"));
        var looping = ArrangeLoop(h, project, pipeline);

        await h.Sut.ResumeAsync(
            [PipelineCommand.Simple(Loop)], project, pipeline, 500, CancellationToken.None);

        h.ExecutorMock.Verify(e => e.ExecuteAsync(looping, It.IsAny<CancellationToken>()),
            Times.Exactly(3), "500 steps already spent do not exhaust the resumed segment");
    }

    private static PipelineContext WithBudget(StepBudget budget)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.StepBudget, budget);
        return pipeline;
    }

    /// <summary>A command that publishes a budget from inside the run, as PhaseSequence does.</summary>
    private static ICommandContext Publisher(
        PipelineExecutorTestBuilder h, ResolvedProject project, PipelineContext pipeline,
        StepBudget budget)
    {
        var context = new Mock<ICommandContext>().Object;
        h.FactoryMock
            .Setup(f => f.Create(It.Is<PipelineCommand>(c => c.Name == "Publisher"), project, pipeline))
            .Returns(context);
        h.ExecutorMock
            .Setup(e => e.ExecuteAsync(context, It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                pipeline.Set(ContextKeys.StepBudget, budget);
                return Task.FromResult(CommandResult.Ok("published"));
            });
        return context;
    }

    /// <summary>A command that splices a copy of itself after itself, forever.</summary>
    private static ICommandContext ArrangeLoop(
        PipelineExecutorTestBuilder h, ResolvedProject project, PipelineContext pipeline) =>
        Arrange(h, Loop, project, pipeline,
            CommandResult.OkAndContinueWith("again", PipelineCommand.Simple(Loop)));

    private static ICommandContext Arrange(
        PipelineExecutorTestBuilder h, string name, ResolvedProject project,
        PipelineContext pipeline, CommandResult result)
    {
        var context = new Mock<ICommandContext>().Object;
        h.FactoryMock
            .Setup(f => f.Create(It.Is<PipelineCommand>(c => c.Name == name), project, pipeline))
            .Returns(context);
        h.ExecutorMock
            .Setup(e => e.ExecuteAsync(context, It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        return context;
    }
}
