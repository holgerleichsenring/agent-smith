using AgentSmith.Application.Services.Branches;
using AgentSmith.Contracts.Providers;
using FluentAssertions;

namespace AgentSmith.Tests.Branches;

/// <summary>2026-11-17-713ec: only a branch whose pull request ended and that has not moved since is deleted.</summary>
public sealed class WorkBranchVerdictTests
{
    private static readonly WorkBranchTip Tip = new("agent-smith/42", "abc");

    [Fact]
    public void Verdict_MergedAtItsTip_IsDeleted() =>
        WorkBranchVerdict.KeepBecause(Tip, Prs(new BranchPullRequest(BranchPullRequestState.Merged, "ABC"))).Should().BeNull();

    [Fact]
    public void Verdict_ClosedAtItsTip_IsDeleted() =>
        WorkBranchVerdict.KeepBecause(Tip, Prs(new BranchPullRequest(BranchPullRequestState.Closed, "abc"))).Should().BeNull();

    [Fact]
    public void Verdict_AnOpenPullRequestIntoIt_KeepsARung() =>
        WorkBranchVerdict.KeepBecause(Tip, new BranchPullRequests([new(BranchPullRequestState.Merged, "abc")], OpenInto: 1))
            .Should().Be(WorkBranchVerdict.TargetedByAnOpenPullRequest);

    [Fact]
    public void Verdict_AnOpenPullRequestFromIt_Keeps() =>
        WorkBranchVerdict.KeepBecause(Tip, Prs(new BranchPullRequest(BranchPullRequestState.Merged, "abc"), new BranchPullRequest(BranchPullRequestState.Open, "abc")))
            .Should().Be(WorkBranchVerdict.HasAnOpenPullRequest);

    [Fact]
    public void Verdict_NoPullRequest_Keeps() =>
        WorkBranchVerdict.KeepBecause(Tip, Prs()).Should().Be(WorkBranchVerdict.NoPullRequest);

    [Fact]
    public void Verdict_PushedToAfterTheMerge_Keeps() =>
        WorkBranchVerdict.KeepBecause(Tip, Prs(new BranchPullRequest(BranchPullRequestState.Merged, "old"))).Should().Be(WorkBranchVerdict.MovedSince);

    private static BranchPullRequests Prs(params BranchPullRequest[] from) => new(from, OpenInto: 0);
}
