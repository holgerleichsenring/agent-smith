using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Services.Rework;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Rework;

/// <summary>2026-10-08-e8b9b: parked by the CURRENT status or an unmoved fact; moved only out of a
/// non-trigger status.</summary>
public sealed class ReworkParkCheckTests
{
    private static ResolvedProject Project(params string[] triggerStatuses) => new()
    {
        Name = "p", Tracker = new TrackerConnection { Name = "jira", Type = TrackerType.Jira },
        JiraTrigger = new JiraTriggerConfig { TriggerStatuses = [.. triggerStatuses], DoneStatus = "Done", FailedStatus = "Failed" },
    };

    private static ReworkParkCheck Check(string status, bool unmoved = false)
    {
        var provider = new Mock<ITicketProvider>();
        provider.Setup(p => p.GetTicketAsync(It.IsAny<TicketId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Ticket(new TicketId("T-1"), "t", "d", null, status, "jira"));
        var factory = new Mock<ITicketProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(provider.Object);
        var store = new Mock<IUnmovedTicketStore>();
        if (unmoved)
            store.Setup(s => s.FindStandingAsync("p", "T-1", "jira", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AgentSmith.Contracts.Models.UnmovedTicketFact("p", "T-1", "jira", "Done", default));
        return new ReworkParkCheck(factory.Object, store.Object);
    }

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
    public async Task Check_VerdictParkStatus_NotParked() =>
        (await Check("Not implementable").CheckAsync(Project("To Do"), "T-1", CancellationToken.None))
            .Parked.Should().BeFalse();
}
