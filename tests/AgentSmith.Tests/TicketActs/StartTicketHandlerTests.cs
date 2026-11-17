using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Filing;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.TicketActs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Tests.SpecDialog.Continuation;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.TicketActs;

/// <summary>2026-11-17-e061b: the 'start' run's step makes an existing ticket claimable and says how it started.</summary>
public sealed class StartTicketHandlerTests
{
    [Fact]
    public async Task StartTicket_NoTriggerConfigured_AnswersNotStartedWithTheReason()
    {
        var project = new ResolvedProject { Name = "alpha", Tracker = new TrackerConnection { Name = "gh", Type = TrackerType.GitHub } };
        var tracker = new FakeTicketTracker();
        var (pipeline, slot) = (new PipelineContext(), new ActSlot<FiledWorkStart>());
        pipeline.Set(ContextKeys.StartTicketRequest, new StartTicketRequest("42", MayStartRuns: true));
        pipeline.Set(ContextKeys.ActOutcome, slot);

        var result = await new StartTicketHandler(FiledWorkDoubles.Move(), tracker).ExecuteAsync(new StartTicketContext(pipeline, project), default);

        result.IsSuccess.Should().BeTrue("a ticket that is not started is an answer, not a failed step");
        slot.Value!.State.Should().Be(FiledStartState.NotStarted);
        slot.Value.Reason.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task StartTicket_NoRequestSeeded_FailsNamingTheKey()
    {
        var result = await new StartTicketHandler(FiledWorkDoubles.Move(), Mock.Of<ITicketProviderFactory>())
            .ExecuteAsync(new StartTicketContext(new PipelineContext(), new ResolvedProject { Name = "alpha" }), default);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain(nameof(ContextKeys.StartTicketRequest));
    }
}
