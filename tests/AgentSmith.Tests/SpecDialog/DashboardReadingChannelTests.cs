using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Server.Hubs;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-17-c7aec: a progress line is worth less than the read it announces, so the
/// channel never fails one. The pushes themselves are proven through the real composition in
/// the harness's SpecDialogReadingTests.
/// </summary>
public sealed class DashboardReadingChannelTests
{
    [Fact]
    public async Task PushAsync_HubThrows_DoesNotThrow()
    {
        var sut = Channel(new InvalidOperationException("hub is down"));

        var act = () => sut.PushAsync("d-1", "repo-a", SourceScopeProgress.Opening, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PushAsync_ACancellationTheCallerDidNotAskFor_DoesNotThrow()
    {
        var sut = Channel(new TaskCanceledException("the push timed out"));

        var act = () => sut.PushAsync("d-1", "repo-a", SourceScopeProgress.Opening, CancellationToken.None);

        await act.Should().NotThrowAsync("only the caller's own cancellation may end the read");
    }

    [Fact]
    public void Observe_ChatPlatformSession_SetsNoObserver()
    {
        var observers = new AsyncLocalSourceScopeObserverAccessor();
        var sut = new DashboardReadingChannel(observers, NullLogger<DashboardReadingChannel>.Instance);

        using var observing = sut.Observe(new ConversationState
        {
            JobId = "s-1", ChannelId = "C-1", UserId = "U-1", Platform = "slack",
            Project = "p", TicketId = string.Empty, StartedAt = DateTimeOffset.UtcNow,
        });

        observing.Should().BeNull();
        observers.Current.Should().BeNull();
    }

    private static DashboardReadingChannel Channel(Exception thrown)
    {
        var hub = new Mock<IHubContext<JobsHub>>();
        hub.Setup(h => h.Clients).Throws(thrown);
        return new DashboardReadingChannel(
            new AsyncLocalSourceScopeObserverAccessor(), NullLogger<DashboardReadingChannel>.Instance, hub.Object);
    }
}
