using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Server.Hubs;
using AgentSmith.Server.Models;
using Microsoft.AspNetCore.SignalR;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-27-481be: a ticket read says so while it is happening.
/// <para>
/// The channel reports what a design turn is READING. It was shaped for source-scope sandboxes —
/// a repository name and a sandbox's own progress — so a ticket had no way to travel on it, and
/// the two tracker round trips a bound conversation makes were pauses with nothing on screen.
/// </para>
/// </summary>
public sealed class TicketReadIsShownTests
{
    [Fact]
    public async Task SpecDialogTicketRead_OnADialog_ReportsOpeningThenReady()
    {
        var said = new List<(string Kind, string Name, string State)>();
        var reports = new TicketReadReports(Channel(said));

        var answer = await reports.AroundAsync("d-1", "DPG-1239", () => Task.FromResult(7), default);

        answer.Should().Be(7);
        said.Should().Equal([("ticket", "DPG-1239", "opening"), ("ticket", "DPG-1239", "ready")]);
    }

    [Fact]
    public async Task SpecDialogTicketRead_ThatFailed_SaysSoAndStillThrows()
    {
        var said = new List<(string Kind, string Name, string State)>();
        var reports = new TicketReadReports(Channel(said));

        var thrown = async () => await reports.AroundAsync<int>(
            "d-1", "DPG-1239", () => throw new InvalidOperationException("unreachable"), default);

        // The report must not swallow the failure: the caller decides what an unread ticket means.
        await thrown.Should().ThrowAsync<InvalidOperationException>();
        said.Select(s => s.State).Should().Equal(["opening", "failed"]);
    }

    [Fact]
    public async Task SpecDialogTicketRead_OnAnyOtherPlatform_ReportsNothing()
    {
        var said = new List<(string Kind, string Name, string State)>();
        var reports = new TicketReadReports(Channel(said));

        // A chat conversation's thread id is a dialog id nobody has joined — the same rule the
        // observer applies to repository reads.
        await reports.AroundAsync(null, "DPG-1239", () => Task.FromResult(7), default);

        said.Should().BeEmpty();
    }

    /// <summary>
    /// The REAL channel over a mocked hub, because the channel is sealed and because what matters
    /// is the push that leaves it — a double in front of it would assert my own arithmetic.
    /// </summary>
    private static DashboardReadingChannel Channel(List<(string, string, string)> said)
    {
        var proxy = new Mock<IClientProxy>();
        proxy.Setup(p => p.SendCoreAsync(
                It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Callback<string, object?[], CancellationToken>((_, args, _) =>
            {
                var push = (SpecDialogReadingPush)args[0]!;
                said.Add((push.Kind, push.Name, push.State));
            })
            .Returns(Task.CompletedTask);
        var clients = new Mock<IHubClients>();
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(proxy.Object);
        var hub = new Mock<IHubContext<JobsHub>>();
        hub.SetupGet(h => h.Clients).Returns(clients.Object);
        return new DashboardReadingChannel(
            new Mock<ISourceScopeObserverAccessor>().Object,
            NullLogger<DashboardReadingChannel>.Instance, hub.Object);
    }
}
