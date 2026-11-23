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

    private static PrReviewRecordView Record(string number, string state) =>
        new($"k{number}", $"https://github.com/o/api/pull/{number}", "api", number, "alpha", "1", "run", state, null,
            DateTimeOffset.UnixEpoch, null, []);
}
