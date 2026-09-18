using AgentSmith.Server.Models;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-17-042ee: where a dialog push goes and whether it goes at all, asked once instead
/// of three times. The outcome, reading and activity channels all read a session this way.
/// </summary>
public sealed class DialogTargetTests
{
    [Fact]
    public void Of_ASessionWithAThread_IsAddressedByItsThread()
        => DialogTarget.Of(Session("slack", thread: "th-9")).Should().Be("th-9");

    [Fact]
    public void Of_ASessionWithNoThread_IsAddressedByItsChannel()
        => DialogTarget.Of(Session("slack", thread: null)).Should().Be("C-1");

    [Fact]
    public void Of_ASessionWithAnEmptyThread_IsAddressedByItsChannel()
        => DialogTarget.Of(Session("slack", thread: "")).Should().Be("C-1");

    [Theory]
    [InlineData(DispatcherDefaults.PlatformDashboard, true)]
    [InlineData("DASHBOARD", true)]
    [InlineData("slack", false)]
    [InlineData("teams", false)]
    public void IsDashboard_ReadsThePlatformWithoutRegardToCase(string platform, bool expected)
        => DialogTarget.IsDashboard(Session(platform, thread: "th-9")).Should().Be(expected);

    private static ConversationState Session(string platform, string? thread) => new()
    {
        JobId = "s-1", ChannelId = "C-1", UserId = "U-1", Platform = platform,
        Project = "p", TicketId = string.Empty, StartedAt = DateTimeOffset.UtcNow,
        ThreadId = thread,
    };
}
