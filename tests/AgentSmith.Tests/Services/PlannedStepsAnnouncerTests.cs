using AgentSmith.Application.Services.Pipeline;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Runs;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

/// <summary>
/// p0405: the executor REPORTS the sequence it is going to run — the one component
/// that holds it — instead of a reader re-deriving it from a preset and a count.
/// Announced when the list is established and again on every splice, never per step.
/// </summary>
public sealed class PlannedStepsAnnouncerTests
{
    private const string RunId = "2026-08-13T12-00-00-0001";

    [Fact]
    public async Task AnnounceChanged_FirstCall_PublishesTheWholeSequenceWithAbsoluteIndexes()
    {
        var events = EventTestStubs.Recording();
        var sut = new PlannedStepsAnnouncer(events);

        await sut.AnnounceChangedAsync(Context(), 1, Commands(), lastAnnounced: null, CancellationToken.None);

        var announced = events.Events.OfType<PipelineStepsPlannedEvent>().Should().ContainSingle().Subject;
        announced.FirstStepIndex.Should().Be(1);
        var steps = RunStoryJson.TryDeserialize<List<PlannedStepView>>(announced.StepsJson)!;
        steps.Select(s => s.StepIndex).Should().Equal(1, 2, 3);
        steps.Select(s => s.CommandName).Should().Equal(
            CommandNames.FetchTicket, CommandNames.AgenticMaster, CommandNames.VerifyPhase);
        steps[1].PhaseId.Should().Be("p19106a");
        steps[1].DisplayName.Should().NotStartWith("p19106a:",
            "the phase rides in its own field, so the label does not repeat it");
    }

    [Fact]
    public async Task AnnounceChanged_UnchangedSequence_PublishesNothingFurther()
    {
        var events = EventTestStubs.Recording();
        var sut = new PlannedStepsAnnouncer(events);
        var context = Context();

        var first = await sut.AnnounceChangedAsync(context, 1, Commands(), null, CancellationToken.None);
        await sut.AnnounceChangedAsync(context, 1, Commands(), first, CancellationToken.None);

        events.Events.OfType<PipelineStepsPlannedEvent>().Should().ContainSingle(
            "a 45-step run pays for its announcements, not for one per step");
    }

    [Fact]
    public async Task AnnounceChanged_AfterASplice_PublishesTheNewSequence()
    {
        var events = EventTestStubs.Recording();
        var sut = new PlannedStepsAnnouncer(events);
        var context = Context();
        var first = await sut.AnnounceChangedAsync(context, 1, Commands(), null, CancellationToken.None);

        var spliced = Commands().Append(
            new PipelineCommand(CommandNames.WritePhaseRecord) { PhaseId = "p19106a" }).ToList();
        await sut.AnnounceChangedAsync(context, 1, spliced, first, CancellationToken.None);

        var latest = events.Events.OfType<PipelineStepsPlannedEvent>().Last();
        RunStoryJson.TryDeserialize<List<PlannedStepView>>(latest.StepsJson)!
            .Should().HaveCount(4);
    }

    [Fact]
    public async Task AnnounceChanged_NoRunId_PublishesNothing()
    {
        var events = EventTestStubs.Recording();
        var sut = new PlannedStepsAnnouncer(events);

        await sut.AnnounceChangedAsync(new PipelineContext(), 1, Commands(), null, CancellationToken.None);

        events.Events.Should().BeEmpty("an event stream needs a run to belong to");
    }

    private static PipelineContext Context()
    {
        var context = new PipelineContext();
        context.Set(ContextKeys.RunId, RunId);
        return context;
    }

    private static List<PipelineCommand> Commands() =>
    [
        PipelineCommand.Simple(CommandNames.FetchTicket),
        new PipelineCommand(CommandNames.AgenticMaster) { PhaseId = "p19106a" },
        new PipelineCommand(CommandNames.VerifyPhase) { PhaseId = "p19106a" },
    ];
}
