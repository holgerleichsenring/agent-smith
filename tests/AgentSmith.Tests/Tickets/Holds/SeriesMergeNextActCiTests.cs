using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Series;
using FluentAssertions;

namespace AgentSmith.Tests.Tickets.Holds;

/// <summary>2026-11-23-849e: the series' merge act refuses a red CI. 2026-11-23-0219: a CI that runs or has not started
/// merges once green under respect_branch_protection (the run arms auto-complete); without it the act names the key.</summary>
public sealed class SeriesMergeNextActCiTests
{
    private static readonly MergePolicyConfig Bypass = new();
    private static readonly MergePolicyConfig Respect = new() { RespectBranchProtection = true };

    private static SeriesRunTicketView Ticket(params string?[] ci) => new("42", "#42", "T", false, 0, [], ["api"], false, "success",
        [.. ci.Select((c, i) => new SeriesRunPullRequestView($"repo{i}", $"{i}", $"u{i}", PrReviewStates.Open, c, null, null, null))]);

    [Fact]
    public void CiHolds_Red_RefusesUnderEitherProtection()
    {
        SeriesMergeNextAct.CiHolds(Ticket(CiStates.Red), Bypass).Should().Be("#42 is not merged: its CI is red.");
        SeriesMergeNextAct.CiHolds(Ticket(CiStates.Pending, CiStates.Red), Respect).Should().Be("#42 is not merged: its CI is red.");
    }

    [Fact]
    public void CiHolds_RunningOrNotStarted_MergesWhenGreenUnderRespect()
    {
        SeriesMergeNextAct.CiHolds(Ticket(CiStates.Green, CiStates.Pending), Respect).Should().BeNull();
        SeriesMergeNextAct.CiHolds(Ticket(CiStates.NotStarted), Respect).Should().BeNull();
    }

    [Fact]
    public void CiHolds_RunningOrNotStartedWithoutRespect_NamesTheKey()
    {
        SeriesMergeNextAct.CiHolds(Ticket(CiStates.Green, CiStates.Pending), Bypass).Should()
            .Contain("its CI is still running").And.Contain("merge.respect_branch_protection: true").And.Contain("Config → Project → Merge");
        SeriesMergeNextAct.CiHolds(Ticket(CiStates.NotStarted), Bypass).Should().Contain("its required build has not started");
    }

    [Fact]
    public void CiHolds_GreenNoneOrUnread_Merges() =>
        SeriesMergeNextAct.CiHolds(Ticket(CiStates.Green, CiStates.None, null), Bypass).Should().BeNull();
}
