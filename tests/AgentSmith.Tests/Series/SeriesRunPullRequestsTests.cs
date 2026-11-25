using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Series;
using FluentAssertions;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-23-31dcb: per repo the open pull request wins over an older ended one; a target is 'elsewhere' only
/// when it and the repo's default branch are both known and differ.</summary>
public sealed class SeriesRunPullRequestsTests
{
    private static readonly ResolvedProject Project = new()
    {
        Name = "alpha", Repos = [new RepoConnection { Name = "api", DefaultBranch = "main" }, new RepoConnection { Name = "web" }],
    };

    private readonly SeriesRunPullRequests _pullRequests = new();

    [Fact]
    public void Of_AnOpenAndAnOlderClosedRecordInOneRepo_ShowsTheOpenOne()
    {
        var shown = _pullRequests.Of([Record("2", PrReviewStates.Open), Record("1", PrReviewStates.Closed)], Project);

        shown.Should().ContainSingle().Which.Number.Should().Be("2");
    }

    [Theory]
    [InlineData("api", "feature/1", true)]
    [InlineData("api", "main", false)]
    [InlineData("api", null, false)]
    [InlineData("web", "feature/1", false)]
    public void TargetsElsewhere_KnownTargetAgainstTheDefaultBranch(string repo, string? target, bool elsewhere)
    {
        var pullRequest = new SeriesRunPullRequestView(repo, "1", "u", PrReviewStates.Open, null, null, null, target);

        _pullRequests.TargetsElsewhere(pullRequest, Project).Should().Be(elsewhere);
    }

    // 2026-11-24-f9c5c: the series' branch is a merge target like the default branch.
    [Fact]
    public void TargetsElsewhere_TheSeriesBranch_IsNotElsewhere()
    {
        var pullRequest = new SeriesRunPullRequestView("api", "1", "u", PrReviewStates.Open, null, null, null, "agent-smith/series/sr-1");

        _pullRequests.TargetsElsewhere(pullRequest, Project, "agent-smith/series/sr-1").Should().BeFalse();
        _pullRequests.TargetsElsewhere(pullRequest, Project).Should().BeTrue("outside its series it is just another branch");
    }

    // 2026-11-25-9992i: who approves a pull request now — standing approvals only, agent-smith's own left out — so the master of
    // an each series sees a person's approval in read_ticket.
    [Fact]
    public void ApprovedBy_NamesTheStandingApprovalsOfPeople()
    {
        var record = Record("1", PrReviewStates.Open) with
        {
            Entries =
            [
                Entry("dana", PrReviewDecisions.Approved, standing: true),
                Entry("lee", PrReviewDecisions.ApprovedWithSuggestions, standing: true),
                Entry("kim", PrReviewDecisions.Approved, standing: false),
                Entry("sam", PrReviewDecisions.ChangesRequested, standing: true),
                Entry("agent", PrReviewDecisions.Approved, standing: true, ours: true),
            ],
        };

        SeriesRunPullRequests.ApprovedBy(record).Should().Equal("dana", "lee");
        _pullRequests.Of([record], Project).Single().ApprovedBy.Should().Equal("dana", "lee");
    }

    private static PrReviewEntryView Entry(string author, string decision, bool standing, bool ours = false) =>
        new($"h-{author}", PrReviewEntryKinds.Decision, decision, standing, author, true, ours, false, false, null, null, null, null,
            string.Empty, false, DateTimeOffset.UnixEpoch);

    private static PrReviewRecordView Record(string number, string state) =>
        new($"k{number}", $"https://github.com/o/api/pull/{number}", "api", number, "alpha", "1", "run", state, null,
            DateTimeOffset.UnixEpoch, null, []);
}
