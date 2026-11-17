using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.TicketActs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.TicketActs;

/// <summary>2026-11-17-e061a: the 'reopen' run's step clears the mark, tells the pages, and says whether there was one.</summary>
public sealed class ReopenTicketHandlerTests
{
    private readonly Mock<ITicketClosedMarkStore> _marks = new();
    private readonly Mock<ITicketActivityNudge> _activity = new();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReopenTicket_ClearsTheMark_AndNudgesOnlyWhenThereWasOne(bool marked)
    {
        _marks.Setup(m => m.ClearAsync("gh", "github-42", It.IsAny<CancellationToken>())).ReturnsAsync(marked);
        var (pipeline, slot) = (new PipelineContext(), new ActSlot<bool>());
        pipeline.Set(ContextKeys.ReopenTicketRequest, new ReopenTicketRequest("42", "github-42"));
        pipeline.Set(ContextKeys.ActOutcome, slot);

        var result = await Handler().ExecuteAsync(new ReopenTicketContext(pipeline, Project()), default);

        result.IsSuccess.Should().BeTrue();
        (slot.IsSet, slot.Value).Should().Be((true, marked));
        _activity.Verify(a => a.ChangedAsync("alpha", "github-42", It.IsAny<CancellationToken>()), marked ? Times.Once() : Times.Never());
    }

    [Fact]
    public async Task ReopenTicket_NoRequestSeeded_FailsAndClearsNothing()
    {
        var result = await Handler().ExecuteAsync(new ReopenTicketContext(new PipelineContext(), Project()), default);

        result.IsSuccess.Should().BeFalse();
        _marks.VerifyNoOtherCalls();
    }

    private ReopenTicketHandler Handler() => new(_marks.Object, _activity.Object, NullLogger<ReopenTicketHandler>.Instance);

    private static ResolvedProject Project() => new()
    {
        Name = "alpha", Tracker = new TrackerConnection { Name = "gh", Type = TrackerType.GitHub },
    };
}
