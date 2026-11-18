using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Application.Services.Persistence;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services.Lifecycle;

/// <summary>2026-11-18-550aa: a done move is recorded the way a park is, and a rejected one is said on the ticket.</summary>
public sealed class TicketLifecycleTests
{
    private static readonly TrackerConnection Tracker = new() { Name = "tracker-a", Type = TrackerType.AzureDevOps };
    private readonly InMemoryUnmovedTicketStore _store = new();
    private readonly Mock<ITicketProvider> _provider = new();

    [Fact]
    public async Task FinalizeAsync_TrackerRejectedTheDoneStatus_HoldsTheTicketAndSaysSo()
    {
        _provider.Setup(p => p.FinalizeAsync(It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TicketFinalizeResult.Rejected("In Review", "RuleValidationException"));

        await FinalizeAsync("In Review");

        (await _store.FindStandingAsync("p1", "7", "tracker-a", CancellationToken.None)).Should().NotBeNull();
        _provider.Verify(p => p.UpdateStatusAsync(It.Is<TicketId>(t => t.Value == "7"), It.Is<string>(c =>
            c.StartsWith("summary") && c.Contains("done_status 'In Review' could not be set: TrackerRejectedTheStatus")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FinalizeAsync_Moved_ClearsAStandingRecordAndPostsNothingMore()
    {
        await _store.RecordAsync(new UnmovedTicketFact("p1", "7", "tracker-a", "Failed",
            TicketFinalizeOutcome.TrackerRejectedTheStatus), CancellationToken.None);
        _provider.Setup(p => p.FinalizeAsync(It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TicketFinalizeResult.Moved());

        await FinalizeAsync("Resolved");

        (await _store.FindStandingAsync("p1", "7", "tracker-a", CancellationToken.None)).Should().BeNull();
        _provider.Verify(p => p.UpdateStatusAsync(It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private Task FinalizeAsync(string doneStatus)
    {
        var factory = new Mock<ITicketProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(_provider.Object);
        var lifecycle = new TicketLifecycle(unmoved: new UnmovedTicketReport(_store, NullLogger<UnmovedTicketReport>.Instance));
        return lifecycle.FinalizeAsync(factory.Object, new TicketFinalizeTarget("p1", Tracker, new TicketId("7")),
            doneStatus, "summary", NullLogger.Instance, CancellationToken.None);
    }
}
