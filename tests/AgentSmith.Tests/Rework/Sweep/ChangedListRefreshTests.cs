using AgentSmith.Infrastructure.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Sweep;
using AgentSmith.Infrastructure.Services.Providers.Source;
using AgentSmith.Infrastructure.Services.Webhooks;
using AgentSmith.Server.Services.Sweep;
using FluentAssertions;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Moq;
using Octokit;

namespace AgentSmith.Tests.Rework.Sweep;

/// <summary>
/// Polling mode's reviews and votes: the changed-PR lists name a change that is no act as
/// RefreshOnly, and the sweep turns it into a Refresh nudge; the act items are unchanged.
/// </summary>
public sealed class ChangedListRefreshTests
{
    private static readonly DateTimeOffset Since = new(2026, 10, 8, 10, 0, 0, TimeSpan.Zero);
    private const string RepoUrl = "https://dev.azure.com/org/proj/_git/r";

    private static async Task<IReadOnlyList<ChangedItem>> AzureAsync(params GitPullRequest[] prs)
    {
        var client = new Mock<GitHttpClient>(new Uri("https://localhost/fake"), new VssCredentials(new VssBasicCredential(string.Empty, "fake")));
        client.Setup(c => c.GetPullRequestsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GitPullRequestSearchCriteria>(),
                It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([.. prs]);
        return (await new AzureReposChangedPullRequests(_ => Task.FromResult(client.Object), "proj", "r", RepoUrl)
            .ChangedSinceAsync(null, 10, CancellationToken.None)).Items;
    }

    private static GitPullRequest Pr(int id, params (string Id, short Vote)[] reviewers) => new()
    {
        PullRequestId = id, SourceRefName = "refs/heads/agent-smith/12", CreatedBy = new IdentityRef { Id = "creator" },
        Reviewers = [.. reviewers.Select(r => new IdentityRefWithVote { Id = r.Id, Vote = r.Vote })],
    };

    [Fact]
    public async Task ChangedPullRequests_AzureDevOpsApproval_RefreshItem_MinusFiveItemUnchanged()
    {
        var items = await AzureAsync(Pr(9, ("b", 10), ("a", -5)), Pr(10, ("creator", 10)));

        items.Select(i => (i.Dedupe, i.RefreshOnly)).Should().Equal(
            ("9|a", false), ("refresh|9|a:-5,b:10", true), ("refresh|10|creator:10", true));
        items.Take(2).Should().OnlyContain(i => i.PrUrl == $"{RepoUrl}/pullrequest/9");
    }

    [Fact]
    public async Task ChangedPullRequests_AzureDevOps_CreatorAtMinusFive_ListsTheVoteItem()
    {
        var items = await AzureAsync(Pr(9, ("creator", -5), ("b", 10)));

        items.Select(i => (i.Dedupe, i.RefreshOnly)).Should().Equal(
            ("9|creator", false), ("refresh|9|b:10,creator:-5", true));
    }

    [Fact]
    public async Task ChangedPullRequests_AzureDevOps_SoleCreatorResetsMinusFive_ListsARefreshItem()
    {
        var waiting = await AzureAsync(Pr(9, ("creator", -5)));
        var reset = (await AzureAsync(Pr(9, ("creator", 0)))).Single();

        reset.RefreshOnly.Should().BeTrue();
        reset.Dedupe.Should().Be("refresh|9|creator:0").And.NotBe(waiting.Single(i => i.RefreshOnly).Dedupe);
    }

    [Fact]
    public async Task ChangedPullRequests_AzureDevOps_SameVoterTwoPrs_BothListed()
    {
        var items = await AzureAsync(Pr(9, ("a", 10)), Pr(10, ("a", 10)));

        items.Select(i => i.Dedupe).Should().Equal("refresh|9|a:10", "refresh|10|a:10");
    }

    [Fact]
    public async Task ChangedPullRequests_AzureDevOps_VoteResetToZero_IsListedAgain()
    {
        var approved = (await AzureAsync(Pr(9, ("a", 10)))).Single().Dedupe;
        var reset = (await AzureAsync(Pr(9, ("a", 0)))).Single().Dedupe;

        reset.Should().Be("refresh|9|a:0").And.NotBe(approved);
    }

    [Fact]
    public async Task ChangedPullRequests_GitHubApproval_IsListedRefreshOnly()
    {
        var response = new Mock<IApiResponse<string>>();
        response.SetupGet(r => r.Body).Returns("""
            { "data": { "repository": { "pullRequests": { "pageInfo": { "hasNextPage": false, "endCursor": null }, "nodes": [
              { "url": "https://github.com/o/r/pull/1", "headRefName": "agent-smith/1", "isCrossRepository": false,
                "latestReviews": { "nodes": [ { "state": "APPROVED", "submittedAt": "2026-10-08T11:00:00Z" },
                                              { "state": "CHANGES_REQUESTED", "submittedAt": "2026-10-08T09:00:00Z" } ] } },
              { "url": "https://github.com/o/r/pull/2", "headRefName": "agent-smith/2", "isCrossRepository": false,
                "latestReviews": { "nodes": [ { "state": "CHANGES_REQUESTED", "submittedAt": "2026-10-08T11:00:00Z" } ] } },
              { "url": "https://github.com/o/r/pull/3", "headRefName": "agent-smith/3", "isCrossRepository": false,
                "latestReviews": { "nodes": [ { "state": "COMMENTED", "submittedAt": "2026-10-08T09:00:00Z" } ] } } ] } } } }
            """);
        var connection = new Mock<IConnection>();
        connection.Setup(c => c.Post<string>(It.IsAny<Uri>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(response.Object);
        var client = new Mock<IGitHubClient>();
        client.SetupGet(c => c.Connection).Returns(connection.Object);

        var page = await new GitHubChangedPullRequests(client.Object, GitHubRepoAddress.Parse("https://github.com/o/r")).ChangedSinceAsync(Since, null, 10, CancellationToken.None);

        page.Items.Select(i => (i.PrUrl, i.RefreshOnly)).Should().Equal(
            ("https://github.com/o/r/pull/1", true), ("https://github.com/o/r/pull/2", false));
    }

    [Fact]
    public async Task ChangeSweep_RefreshOnlyItem_EnqueuesRefreshOrigin()
    {
        var attempts = new Mock<IPreviousAttemptReader>();
        attempts.Setup(a => a.LatestAsync("p", "12", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PreviousAttempt("run-1", "success", Since, true));
        var nudges = new List<ReworkNudgeRequest>();
        var queue = new Mock<IReworkNudges>();
        queue.Setup(n => n.EnqueueAsync(It.IsAny<ReworkNudgeRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ReworkNudgeRequest, CancellationToken>((r, _) => nudges.Add(r)).Returns(Task.CompletedTask);
        var tracker = new TrackerConnection { Name = "t", Polling = new PollingConfig { Enabled = true } };
        var config = new AgentSmithConfig { Projects = new() { ["p"] = new() { Name = "p", Tracker = tracker,
            Repos = [new RepoConnection { Name = "r", Url = "https://github.com/o/r", Type = RepoType.GitHub }] } } };
        var provider = new Mock<ISourceProvider>();
        provider.As<IChangedPullRequestLister>();
        var sources = new Mock<ISourceProviderFactory>();
        sources.Setup(s => s.Create(It.IsAny<RepoConnection>())).Returns(provider.Object);
        var source = new ChangeSweepSources(Mock.Of<ITicketProviderFactory>(), sources.Object, new ConfiguredRepoFinder(), attempts.Object, queue.Object)
            .For(config, [tracker]).Single();

        await source.NudgeAsync(new ChangedItem(Since, PrUrl: "https://github.com/o/r/pull/1", HeadRef: "agent-smith/12", RefreshOnly: true), CancellationToken.None);
        await source.NudgeAsync(new ChangedItem(Since, PrUrl: "https://github.com/o/r/pull/1", HeadRef: "agent-smith/12"), CancellationToken.None);

        nudges.Select(n => n.Origin).Should().Equal(ReworkNudgeOrigin.Refresh, ReworkNudgeOrigin.Sweep);
        nudges[0].IsLate.Should().BeTrue("a sweep's refresh never pulls a backing-off nudge forward");
    }
}
