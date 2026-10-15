using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Rework;
using AgentSmith.Application.Services.Tickets;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Tickets.Closure;

/// <summary>2026-10-13-9a59k: closed for work = the tracker counts it closed AND a person put it there.</summary>
public sealed class TicketClosureTests
{
    private static readonly TrackerActor Token = new("agent-id", "agent");
    private static readonly DateTimeOffset At = new(2026, 10, 13, 9, 0, 0, TimeSpan.Zero);

    private readonly Mock<ITicketProvider> _provider = new();
    private readonly Mock<ITicketProviderFactory> _factory = new();

    public TicketClosureTests()
    {
        _provider.As<ITicketStatusHistory>();
        _provider.As<ITrackerSelf>().Setup(s => s.SelfAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Token);
        _factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(_provider.Object);
    }

    private static ResolvedProject Project(string doneStatus = "In Review") => new()
    {
        Name = "p",
        Tracker = new TrackerConnection { Name = "gh", Type = TrackerType.GitHub, NotImplementableStatus = "wontfix" },
        GithubTrigger = new WebhookTriggerConfig
        {
            DoneStatus = doneStatus, FailedStatus = "Failed", NeedsClarificationStatus = "Clarify",
        },
    };

    private static Ticket Closed(string status) => new(new TicketId("4"), "t", "d", null, status, "GitHub", isClosed: true);

    private void ClosedBy(TrackerActor? actor) =>
        _provider.As<ITicketStatusHistory>()
            .Setup(h => h.NewestMoveIntoAsync(new TicketId("4"), "closed", It.IsAny<CancellationToken>()))
            .ReturnsAsync(actor is null ? null : new TicketStatusMove(actor, At, "closed"));

    private Task<bool> ClosedForWork(ResolvedProject project, Ticket ticket) =>
        new TicketClosure(new TrackerIdentity(_factory.Object, NullLogger<TrackerIdentity>.Instance), NullLogger<TicketClosure>.Instance)
            .ClosedForWorkAsync(project, _provider.Object, ticket, CancellationToken.None);

    [Fact]
    public async Task TicketClosure_OwnDoneStatusInClosedCategory_IsNotClosedForWork()
    {
        (await ClosedForWork(Project(doneStatus: "Done"), Closed("Done"))).Should().BeFalse();
        (await ClosedForWork(Project(), new Ticket(new TicketId("4"), "t", "d", null, "Shipped", "GitHub")))
            .Should().BeFalse("a status the tracker does not count closed is never closed for work");
    }

    [Theory]
    [InlineData("Failed")]
    [InlineData("clarify")]
    [InlineData("wontfix")]
    public async Task TicketClosure_OwnFailedClarificationOrNotImplementable_IsNotClosedForWork(string status) =>
        (await ClosedForWork(Project(), Closed(status))).Should().BeFalse();

    [Fact]
    public async Task TicketClosure_TicketInTheClosedStatus_IsNotClosedForWork()
    {
        var project = Project() with { Tracker = Project().Tracker with { ClosedStatus = "Won't Fix" } };

        (await ClosedForWork(project, Closed("won't fix"))).Should().BeFalse("agent-smith's closed_status is its own");
        (await ClosedForWork(Project(), Closed("Won't Fix"))).Should().BeTrue("without it the same status is a person's");
    }

    [Fact]
    public async Task TicketClosure_PersonMovedToOtherClosedStatus_IsClosedForWork()
    {
        (await ClosedForWork(Project(), Closed("Won't Fix"))).Should().BeTrue();
        _provider.As<ITicketStatusHistory>().Verify(
            h => h.NewestMoveIntoAsync(It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never,
            "with a done_status configured the configured rule decides");
    }

    [Theory]
    [InlineData("agent-id", false)]
    [InlineData("renovate[bot]", true)]
    public async Task TicketClosure_NoDoneStatus_OurClose_IsNotClosedForWork(string id, bool isApp)
    {
        ClosedBy(new TrackerActor(id, id, isApp));

        (await ClosedForWork(Project(doneStatus: ""), Closed("closed"))).Should().BeFalse();
    }

    [Fact]
    public async Task TicketClosure_NoDoneStatus_PersonClose_IsClosedForWork()
    {
        ClosedBy(new TrackerActor("alice", "alice"));

        (await ClosedForWork(Project(doneStatus: ""), Closed("closed"))).Should().BeTrue();
    }

    [Fact]
    public async Task TicketClosure_NoDoneStatus_HistoryUnreadable_IsNotClosedForWork()
    {
        ClosedBy(null);
        (await ClosedForWork(Project(doneStatus: ""), Closed("closed"))).Should().BeFalse("no move found");

        _provider.As<ITicketStatusHistory>()
            .Setup(h => h.NewestMoveIntoAsync(It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("history down"));
        (await ClosedForWork(Project(doneStatus: ""), Closed("closed"))).Should().BeFalse("a failed read");

        ClosedBy(new TrackerActor("alice", "alice"));
        _provider.As<ITrackerSelf>().Setup(s => s.SelfAsync(It.IsAny<CancellationToken>())).ReturnsAsync((TrackerActor?)null);
        (await ClosedForWork(Project(doneStatus: ""), Closed("closed"))).Should().BeFalse("an unknown token identity");
    }

    [Fact]
    public void TicketClosure_ResolvesFromPipelineHandlers()
    {
        var services = new ServiceCollection().AddLogging()
            .AddSingleton(_factory.Object)
            .AddPipelineHandlers();

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<TicketClosure>().Should().NotBeNull();
    }
}
