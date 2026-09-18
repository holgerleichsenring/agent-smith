using AgentSmith.Application.Services.Turns;
using AgentSmith.Server.Hubs;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-17-042ee: a progress line is worth less than the step it announces, so the channel
/// never fails one. The pushes themselves are proven through the real composition in the
/// harness's SpecDialogActivityTests.
/// </summary>
public sealed class DashboardActivityChannelTests
{
    [Fact]
    public async Task SendAsync_HubThrows_DoesNotThrow()
    {
        var sut = Channel(new InvalidOperationException("hub is down"));

        var act = () => sut.SendAsync(Step(), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendAsync_ACancellationTheCallerDidNotAskFor_DoesNotThrow()
    {
        var sut = Channel(new TaskCanceledException("the push timed out"));

        var act = () => sut.SendAsync(Step(), CancellationToken.None);

        await act.Should().NotThrowAsync("only the caller's own cancellation may end the work");
    }

    [Fact]
    public async Task SendAsync_TheCallersOwnCancelledToken_DoesNotThrow()
    {
        var sut = Channel(new TaskCanceledException("the hub saw the cancellation"));
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        var act = () => sut.SendAsync(Step(), cancelled.Token);

        await act.Should().NotThrowAsync(
            "a step is reported from inside work that publishes its own outcome next: a throw "
            + "here is read there as that work failing, and publishes a contrary second event");
    }

    [Fact]
    public void Observe_ChatPlatformSession_SetsNoObserver()
    {
        var observers = new AsyncLocalTurnActivityObserverAccessor();
        var sut = new DashboardActivityChannel(
            observers, NullLogger<DashboardActivityChannel>.Instance);

        using var observing = sut.Observe(Session("slack"), new RunningDialogTurn(TimeProvider.System));

        observing.Should().BeNull();
        observers.Current.Should().BeNull();
    }

    [Fact]
    public void Observe_DashboardSession_SetsAnObserverBoundToTheDialog()
    {
        var observers = new AsyncLocalTurnActivityObserverAccessor();
        var sut = new DashboardActivityChannel(
            observers, NullLogger<DashboardActivityChannel>.Instance);

        using var observing = sut.Observe(
            Session(DispatcherDefaults.PlatformDashboard), new RunningDialogTurn(TimeProvider.System));

        observing.Should().NotBeNull();
        observers.Current.Should().BeOfType<DialogActivityObserver>();
    }

    private static SpecDialogActivityPush Step() =>
        new("d-1", "tool", "read_file", "repo/src/Router.cs", DateTimeOffset.UtcNow, 1,
            DateTimeOffset.UtcNow);

    private static ConversationState Session(string platform) => new()
    {
        JobId = "s-1", ChannelId = "C-1", UserId = "U-1", Platform = platform,
        Project = "p", TicketId = string.Empty, StartedAt = DateTimeOffset.UtcNow,
        ThreadId = "d-1",
    };

    private static DashboardActivityChannel Channel(Exception thrown)
    {
        var hub = new Mock<IHubContext<JobsHub>>();
        hub.Setup(h => h.Clients).Throws(thrown);
        return new DashboardActivityChannel(
            new AsyncLocalTurnActivityObserverAccessor(),
            NullLogger<DashboardActivityChannel>.Instance, hub.Object);
    }
}
