using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Services;

/// <summary>
/// p0312d: the batch path is gone — one command is one step, always. These tests
/// replace PipelineExecutorBatchingTests, which pinned the inert state (nothing was
/// batchable) while the machinery around it still existed. What is worth pinning now
/// is the OBSERVABLE consequence: every command a preset declares produces exactly one
/// StepStarted/StepFinished pair at its own index, so the run rail, the persisted
/// RunStep rows and the step-scoped event attribution cannot silently collapse two
/// commands into one row again.
/// </summary>
public sealed class PipelineSingleStepDispatchTests
{
    // Provisioning is not under test here: every preset would otherwise need a staged
    // repo inventory before the first sandbox-requiring command.
    private static IPipelineSandboxCoordinator NoSandbox()
    {
        var mock = new Mock<IPipelineSandboxCoordinator>();
        mock.Setup(c => c.IsSandboxRequiring(It.IsAny<string>())).Returns(false);
        mock.Setup(c => c.RequiresSandbox(It.IsAny<IEnumerable<PipelineCommand>>())).Returns(false);
        return mock.Object;
    }

    public static TheoryData<string> EveryPreset()
    {
        var data = new TheoryData<string>();
        foreach (var name in PipelinePresets.Names) data.Add(name);
        return data;
    }

    [Theory]
    [MemberData(nameof(EveryPreset))]
    public async Task StepRunner_EveryPreset_PublishesOneStepPerCommand(string presetName)
    {
        var commands = PipelinePresets.TryResolve(presetName)!;
        var events = EventTestStubs.Recording();
        var h = new PipelineExecutorTestBuilder(events, NoSandbox());
        var project = new ResolvedProject();
        var context = new PipelineContext();
        context.Set(ContextKeys.RunId, "run-1");
        h.ExecutorMock
            .Setup(e => e.ExecuteAsync(It.IsAny<ICommandContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CommandResult.Ok("done"));

        var result = await h.Sut.ExecuteAsync(commands, project, context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var started = events.Events.OfType<StepStartedEvent>().ToList();
        var finished = events.Events.OfType<StepFinishedEvent>().ToList();
        started.Should().HaveCount(commands.Count,
            $"preset '{presetName}' declares {commands.Count} commands and each is its own step");
        finished.Should().HaveCount(commands.Count);
        started.Select(e => e.StepIndex).Should().BeEquivalentTo(
            Enumerable.Range(1, commands.Count),
            o => o.WithStrictOrdering(),
            "step indices are one continuous sequence, one per command");
        started.Select(e => e.CommandName).Should().BeEquivalentTo(
            commands, o => o.WithStrictOrdering(),
            "each step carries the typed name of the ONE command it ran");
        started.Should().OnlyContain(e => e.TotalSteps == commands.Count);
    }

    [Fact]
    public async Task StepRunner_RepeatedCommand_StillPublishesOneStepEach()
    {
        // The batch path peeled consecutive same-name commands into a single step.
        // Two identical adjacent commands are exactly the shape it collapsed, so this
        // is the regression that would reappear if a peeler came back.
        var events = EventTestStubs.Recording();
        var h = new PipelineExecutorTestBuilder(events, NoSandbox());
        var context = new PipelineContext();
        context.Set(ContextKeys.RunId, "run-1");
        h.ExecutorMock
            .Setup(e => e.ExecuteAsync(It.IsAny<ICommandContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CommandResult.Ok("done"));

        await h.Sut.ExecuteAsync(
            new[] { CommandNames.AnalyzeCode, CommandNames.AnalyzeCode, CommandNames.AnalyzeCode },
            new ResolvedProject(), context, CancellationToken.None);

        events.Events.OfType<StepStartedEvent>().Should().HaveCount(3);
        events.Events.OfType<StepStartedEvent>().Select(e => e.StepIndex)
            .Should().BeEquivalentTo(new[] { 1, 2, 3 }, o => o.WithStrictOrdering());
    }
}
