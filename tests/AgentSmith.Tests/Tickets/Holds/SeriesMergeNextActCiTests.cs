using AgentSmith.Contracts.Reviews;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Series;
using FluentAssertions;

namespace AgentSmith.Tests.Tickets.Holds;

/// <summary>2026-11-23-849e: the series' merge act refuses what the page no longer offers — a ready step whose CI still runs
/// or is red — by the hold's rule; green, none and unread CI merge.</summary>
public sealed class SeriesMergeNextActCiTests
{
    private static SeriesRunTicketView Ticket(params string?[] ci) => new("42", "#42", "T", false, 0, [], ["api"], false, "success",
        [.. ci.Select((c, i) => new SeriesRunPullRequestView($"repo{i}", $"{i}", $"u{i}", PrReviewStates.Open, c, null, null, null))]);

    [Fact]
    public void CiHolds_RunningOrRed_Refuses_GreenNoneOrUnread_Merges()
    {
        SeriesMergeNextAct.CiHolds(Ticket(CiStates.Green, CiStates.Pending)).Should().Be("#42 is not merged yet: its CI is still running.");
        SeriesMergeNextAct.CiHolds(Ticket(CiStates.Red)).Should().Be("#42 is not merged: its CI is red.");
        SeriesMergeNextAct.CiHolds(Ticket(CiStates.Green, CiStates.None, null)).Should().BeNull();
    }
}
