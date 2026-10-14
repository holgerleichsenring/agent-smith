using AgentSmith.Application.Services.Rework;
using AgentSmith.Application.Services.Tickets;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Services.Rework;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Rework;

/// <summary>2026-10-08-e8b9b: parked by the CURRENT status or an unmoved fact; moved only out of a
/// non-trigger status.</summary>
public sealed class ReworkParkCheckTests
{
    private static ResolvedProject Project(params string[] triggerStatuses) => new()
    {
        Name = "p", Tracker = new TrackerConnection { Name = "jira", Type = TrackerType.Jira, NotImplementableStatus = "Not implementable" },
        JiraTrigger = new JiraTriggerConfig
        {
            TriggerStatuses = [.. triggerStatuses], DoneStatus = "Done", FailedStatus = "Failed", NeedsClarificationStatus = "Clarify",
        },
    };

    private static ReworkParkCheck Check(string status, bool unmoved = false, bool closed = false)
    {
        var provider = new Mock<ITicketProvider>();
        provider.Setup(p => p.GetTicketAsync(It.IsAny<TicketId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Ticket(new TicketId("T-1"), "t", "d", null, status, "jira", isClosed: closed));
        var factory = new Mock<ITicketProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(provider.Object);
        var store = new Mock<IUnmovedTicketStore>();
        if (unmoved)
            store.Setup(s => s.FindStandingAsync("p", "T-1", "jira", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AgentSmith.Contracts.Models.UnmovedTicketFact("p", "T-1", "jira", "Done", default));
        return new ReworkParkCheck(factory.Object, store.Object,
            new TicketClosure(new TrackerIdentity(factory.Object, NullLogger<TrackerIdentity>.Instance), NullLogger<TicketClosure>.Instance));
    }

    // 2026-10-13-9a59t: a ticket a person closed is Closed whatever its status; agent-smith's own done is a park.
    [Fact]
    public async Task ReworkParkCheck_PersonClosedTicket_IsClosed() =>
        (await Check("Closed", closed: true).CheckAsync(Project("To Do"), "T-1", CancellationToken.None))
            .Should().Be(new ReworkPark(false, null, Closed: true));

    [Fact]
    public async Task ReworkParkCheck_OwnDoneStatusInClosedCategory_IsParkedNotClosed() =>
        (await Check("Done", closed: true).CheckAsync(Project("To Do"), "T-1", CancellationToken.None))
            .Should().Be(new ReworkPark(true, "To Do"));

    [Fact]
    public async Task Check_DoneStatus_ParkedAndMovedToFirstTrigger() =>
        (await Check("Done").CheckAsync(Project("To Do", "Open"), "T-1", CancellationToken.None))
            .Should().Be(new ReworkPark(true, "To Do"));

    [Fact]
    public async Task Check_UnmovedFactOnTriggerStatus_ParkedWithoutMove() =>
        (await Check("To Do", unmoved: true).CheckAsync(Project("To Do"), "T-1", CancellationToken.None))
            .Should().Be(new ReworkPark(true, null));

    [Fact]
    public async Task ReworkEntry_EmptyTriggerList_ClaimsWithoutMove() =>
        (await Check("Failed").CheckAsync(Project(), "T-1", CancellationToken.None))
            .Should().Be(new ReworkPark(true, null));

    [Fact]
    public async Task Check_TriggerStatus_ReworkableWithoutMove() =>
        (await Check("open").CheckAsync(Project("open"), "T-1", CancellationToken.None))
            .Should().Be(new ReworkPark(true, null));

    [Fact]
    public async Task Check_VerdictParkStatus_NotParked() =>
        (await Check("Not implementable").CheckAsync(Project("To Do"), "T-1", CancellationToken.None))
            .Parked.Should().BeFalse();

    [Theory]
    [InlineData("Clarify")]
    [InlineData("Not implementable")]
    public async Task CheckHandback_HandbackParkStatus_ParkedAndMoved(string status) =>
        (await Check(status).CheckHandbackAsync(Project("To Do"), "T-1", CancellationToken.None))
            .Should().Be(new ReworkPark(true, "To Do"));

    [Fact]
    public async Task Check_ClarificationStatus_NotParkedWithoutHandback() =>
        (await Check("Clarify").CheckAsync(Project("To Do"), "T-1", CancellationToken.None))
            .Parked.Should().BeFalse();

    [Theory]
    [InlineData("Tracker clarify", true)]
    [InlineData("Trigger not implementable", true)]
    [InlineData("Tracker not implementable", false)]
    public async Task ReworkParkCheck_HandbackPark_UnchangedThroughSharedResolver(string status, bool parked)
    {
        var project = new ResolvedProject
        {
            Name = "p",
            Tracker = new TrackerConnection
            {
                Name = "jira", Type = TrackerType.Jira,
                NeedsClarificationStatus = "Tracker clarify", NotImplementableStatus = "Tracker not implementable",
            },
            JiraTrigger = new JiraTriggerConfig
            {
                TriggerStatuses = ["To Do"], DoneStatus = "Done", NotImplementableStatus = "Trigger not implementable",
            },
        };

        (await Check(status).CheckHandbackAsync(project, "T-1", CancellationToken.None))
            .Should().Be(new ReworkPark(parked, parked ? "To Do" : null),
                "the trigger's status wins over the tracker's; the tracker's fills a blank trigger");
    }
}
