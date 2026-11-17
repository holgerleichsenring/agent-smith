using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.TicketActs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Services.Tickets.Closing;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.TicketActs;

/// <summary>2026-11-17-e061a: the 'close' run's step — host acts, then the mark when asked, all into the outcome slot.</summary>
public sealed class CloseTicketHandlerTests
{
    private readonly Mock<ITicketProvider> _tracker = new();
    private readonly Mock<IPullRequestCloser> _closer = new();
    private readonly Mock<ITicketClosedMarkStore> _marks = new();
    private readonly Mock<IReworkNudges> _nudges = new();

    private static readonly ResolvedProject Project = new()
    {
        Name = "alpha", Tracker = new TrackerConnection { Name = "gh", Type = TrackerType.GitHub },
        Repos = [new RepoConnection { Name = "api" }, new RepoConnection { Name = "local" }],
    };

    [Fact]
    public async Task CloseTicket_PersonsClose_ClosesTheOpenPullRequestTheTrackerAndStoresTheMarkOfWhatClosed()
    {
        _tracker.Setup(t => t.CloseTicketAsync(new TicketId("42"), "resolution", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var (result, slot) = await RunAsync(Request(new TicketCloseMarking("github-42", "Ada", "why"),
            new("https://h/api/1", "api", false), new("https://h/api/2", "api", true), new("https://h/local/3", "local", false)));

        result.IsSuccess.Should().BeTrue();
        slot.Value!.PullRequests.Select(p => (p.Url, p.Closed, p.Reason)).Should().Equal(
            ("https://h/api/1", true, null), ("https://h/api/2", true, TicketCloseReasons.AlreadyClosed),
            ("https://h/local/3", false, TicketCloseReasons.Unsupported));
        slot.Value.Tracker.Should().Be(new TicketTrackerCloseOutcome(true, null));
        slot.Value.Mark!.ClosedPullRequests.Should().Equal("https://h/api/1");
        _closer.Verify(c => c.CloseAsync("https://h/api/1", It.IsAny<CancellationToken>()), Times.Once);
        _marks.Verify(m => m.SetAsync(It.Is<TicketClosedMark>(k => k.TrackerClosed && k.By == "Ada"), It.IsAny<CancellationToken>()), Times.Once);
        _nudges.Verify(n => n.EnqueueAsync(It.Is<ReworkNudgeRequest>(r => r.Origin == ReworkNudgeOrigin.Refresh), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CloseTicket_Withdrawal_ClosesTheTrackerAndStoresNoMark()
    {
        _tracker.Setup(t => t.CloseTicketAsync(new TicketId("42"), "resolution", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var (_, slot) = await RunAsync(Request(mark: null));

        slot.Value!.Tracker.Should().Be(new TicketTrackerCloseOutcome(false, TicketCloseReasons.Refused));
        slot.Value.Mark.Should().BeNull();
        _marks.Verify(m => m.SetAsync(It.IsAny<TicketClosedMark>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CloseTicket_NoRequestSeeded_FailsNamingTheKeyAndClosesNothing()
    {
        var context = new CloseTicketContext(new PipelineContext(), Project);

        var result = await Handler().ExecuteAsync(context, default);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain(nameof(ContextKeys.CloseTicketRequest));
        _tracker.VerifyNoOtherCalls();
    }

    private static CloseTicketRequest Request(TicketCloseMarking? mark, params TicketPullRequestToClose[] prs) =>
        new("42", "resolution", prs, CloseOnTracker: true, mark);

    private async Task<(CommandResult Result, ActSlot<TicketCloseOutcome> Slot)> RunAsync(CloseTicketRequest request)
    {
        var (pipeline, slot) = (new PipelineContext(), new ActSlot<TicketCloseOutcome>());
        pipeline.Set(ContextKeys.CloseTicketRequest, request);
        pipeline.Set(ContextKeys.ActOutcome, slot);
        return (await Handler().ExecuteAsync(new CloseTicketContext(pipeline, Project), default), slot);
    }

    private CloseTicketHandler Handler()
    {
        var host = new Mock<ISourceProvider>();
        host.As<IPullRequestCloser>().Setup(c => c.CloseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>((url, ct) => _closer.Object.CloseAsync(url, ct));
        var sources = new Mock<ISourceProviderFactory>();
        sources.Setup(s => s.Create(It.Is<RepoConnection>(r => r.Name == "api"))).Returns(host.Object);
        sources.Setup(s => s.Create(It.Is<RepoConnection>(r => r.Name == "local"))).Returns(Mock.Of<ISourceProvider>());
        var trackers = new Mock<ITicketProviderFactory>();
        trackers.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(_tracker.Object);
        var hosts = new TicketHostClose(sources.Object, trackers.Object, new TicketCloseFailureClassifier(), NullLogger<TicketHostClose>.Instance);
        return new CloseTicketHandler(new TicketCloseActs(hosts, _marks.Object, _nudges.Object, Mock.Of<ITicketActivityNudge>(),
            TimeProvider.System, NullLogger<TicketCloseActs>.Instance));
    }
}
