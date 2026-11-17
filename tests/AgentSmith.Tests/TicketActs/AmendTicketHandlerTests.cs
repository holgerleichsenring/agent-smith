using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Filing;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.TicketActs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.TicketActs;

/// <summary>2026-11-17-e061c: the 'amend' run's step answers through its slot; a write that throws is told, not thrown.</summary>
public sealed class AmendTicketHandlerTests
{
    [Fact]
    public async Task AmendTicket_TheWriteThrows_SlotSaysNotRecorded_AndTheStepFails()
    {
        var trackers = new Mock<ITicketProviderFactory>();
        trackers.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Throws(new InvalidOperationException("tracker gone"));
        trackers.Setup(f => f.CreateRewriter(It.IsAny<TrackerConnection>())).Throws(new InvalidOperationException("tracker gone"));
        var store = new Mock<AgentSmith.Contracts.Specs.ISpecApprovalStore>();
        store.Setup(s => s.SaveAsync(It.IsAny<AgentSmith.Contracts.Specs.SpecApprovalRecord>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("store gone"));
        var write = new TicketAmendmentWrite(ApprovedSetDoubles.Recorder(store.Object), trackers.Object, ApprovedSetDoubles.Branch(),
            new TicketDescriptionRewrite(trackers.Object, NullLogger<TicketDescriptionRewrite>.Instance), NullLogger<TicketAmendmentWrite>.Instance);
        var (pipeline, slot) = (new PipelineContext(), new ActSlot<TicketAmendmentResult>());
        pipeline.Set(ContextKeys.AmendTicketRequest, new AmendTicketRequest(
            new ApprovalSource("s-1", "sample.user", [], []), "42", new FiledSeries("2026-11-17-aaaa", []), "goal", "region", null, null));
        pipeline.Set(ContextKeys.ActOutcome, slot);
        var project = new ResolvedProject { Name = "alpha", Tracker = new TrackerConnection { Name = "gh", Type = TrackerType.GitHub } };

        var result = await new AmendTicketHandler(write, NullLogger<AmendTicketHandler>.Instance)
            .ExecuteAsync(new AmendTicketContext(pipeline, project), default);

        result.IsSuccess.Should().BeFalse();
        slot.Value!.Recorded.Should().BeFalse();
        slot.Value.Notice.Should().Contain("did not complete");
    }

    [Fact]
    public async Task AmendTicket_NoRequestSeeded_FailsNamingTheKey()
    {
        var handler = new AmendTicketHandler(null!, NullLogger<AmendTicketHandler>.Instance);

        var result = await handler.ExecuteAsync(new AmendTicketContext(new PipelineContext(), new ResolvedProject { Name = "alpha" }), default);

        result.Message.Should().Contain(nameof(ContextKeys.AmendTicketRequest));
    }
}
