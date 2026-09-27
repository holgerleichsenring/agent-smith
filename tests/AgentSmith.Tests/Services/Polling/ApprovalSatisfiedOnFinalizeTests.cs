using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Application.Services.Persistence;
using AgentSmith.Application.Services.Polling;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services.Polling;

/// <summary>
/// 2026-09-25-c1f7: finalizing a ticket is where a run SATISFIES its approved record — the one
/// place both success paths move a ticket into its done status. Without it the record stays
/// outstanding forever and every later discovery query keeps naming a ticket that is finished.
/// </summary>
public sealed class ApprovalSatisfiedOnFinalizeTests
{
    private static readonly TrackerConnection Tracker =
        new() { Name = ApprovedSets.Tracker, Type = TrackerType.Jira };

    private static readonly DateTimeOffset Finished = ApprovedSets.Noon.AddHours(3);

    [Fact]
    public async Task Finalize_AnApprovedTicket_StopsTheRecordWideningTheNextQuery()
    {
        var store = await StoreWithAsync("DPG-1239");

        await FinalizeAsync(store, "DPG-1239");

        (await store.ListOutstandingAsync(Tracker.Name, 10, CancellationToken.None))
            .TicketIds.Should().BeEmpty();
    }

    [Fact]
    public async Task Finalize_ATicketNobodyApproved_ChangesNothingAndDoesNotThrow()
    {
        var store = await StoreWithAsync("DPG-1239");

        await FinalizeAsync(store, "DPG-7");

        (await store.ListOutstandingAsync(Tracker.Name, 10, CancellationToken.None))
            .TicketIds.Should().BeEquivalentTo(["DPG-1239"],
                "a run finalizing an unapproved ticket is the ordinary case, not an error");
    }

    /// <summary>The CLI and the handler tests build this class with no store at all.</summary>
    [Fact]
    public async Task Finalize_WithNoApprovalStore_StillFinalizesTheTicket()
    {
        var provider = Provider();

        await new TicketLifecycle().FinalizeAsync(
            Factory(provider), Tracker, new TicketId("DPG-1239"), "Done", "summary",
            NullLogger.Instance, CancellationToken.None);

        provider.Verify(p => p.FinalizeAsync(
            It.IsAny<TicketId>(), "summary", "Done", It.IsAny<CancellationToken>()), Times.Once);
    }

    private static async Task FinalizeAsync(ISpecApprovalStore store, string ticketId) =>
        await new TicketLifecycle(store, new StoppedClock()).FinalizeAsync(
            Factory(Provider()), Tracker, new TicketId(ticketId), "Done", "summary",
            NullLogger.Instance, CancellationToken.None);

    private static async Task<InMemorySpecApprovalStore> StoreWithAsync(string ticketId)
    {
        var store = new InMemorySpecApprovalStore();
        await store.SaveAsync(
            ApprovedSets.Record(
                SpecSetKey.For("jira", ticketId).Value, ApprovedSets.Noon, ticketId: ticketId),
            CancellationToken.None);
        return store;
    }

    private static Mock<ITicketProvider> Provider()
    {
        var provider = new Mock<ITicketProvider>();
        provider.Setup(p => p.FinalizeAsync(
                It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TicketFinalizeResult.Moved());
        return provider;
    }

    /// <summary>The satisfaction instant is asserted nowhere here, but a run's clock is not the
    /// wall clock and this class must not read one.</summary>
    private sealed class StoppedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Finished;
    }

    private static ITicketProviderFactory Factory(Mock<ITicketProvider> provider)
    {
        var factory = new Mock<ITicketProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(provider.Object);
        return factory.Object;
    }
}
