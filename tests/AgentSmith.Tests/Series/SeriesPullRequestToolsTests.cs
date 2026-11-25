using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Series;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-25-9992e: the series master merges (now or auto), refreshes, retargets and reads or restarts the CI of a
/// member ticket's pull requests through its acts, and of the final pull requests with ticket "final"; a write needs the
/// approver's runs.control, a read does not.</summary>
public sealed class SeriesPullRequestToolsTests
{
    private const string Branch = "agent-smith/series/sr-1";
    private const string Final = "https://github.com/example/api/pull/90";
    private readonly Mock<ISeriesPullRequestActs> _acts = new();
    private readonly Mock<ISourceProvider> _host = new();
    private readonly Mock<ISeriesRunLaunch> _launch = new();

    private SeriesPullRequestTools Tools(bool mayStartRuns = true)
    {
        var record = SeriesRecords.Running(mayStartRuns: mayStartRuns) with
        {
            IntegrationBranch = Branch, Deliveries = [new SeriesDelivery("api", Final, "90")],
        };
        var project = new ResolvedProject
        {
            Name = "sample", Repos = [new RepoConnection { Name = "api", Type = RepoType.GitHub, Url = "https://github.com/example/api" }],
        };
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, "beat-1");
        var sources = Mock.Of<ISourceProviderFactory>(f => f.Create(It.IsAny<RepoConnection>()) == _host.Object);
        var beat = new SeriesBeat(record, [.. record.Tickets.Select(t => new SeriesTicketFact(t))], SeriesRecords.T);
        return new SeriesPullRequestTools(beat, new SeriesMasterContext(pipeline, project), _acts.Object,
            SeriesFinalDoubles.Merge(sources, _launch.Object), SeriesFinalDoubles.Ci(sources));
    }

    [Theory]
    [InlineData("now", BranchProtection.Bypass)]
    [InlineData("auto", BranchProtection.Respect)]
    public async Task MergePullRequest_ATicket_ReachesTheMergeActWithItsProtection(string how, BranchProtection protection)
    {
        _acts.Setup(a => a.MergeAsync(It.IsAny<SeriesRunRecord>(), "44", protection, It.IsAny<CancellationToken>())).ReturnsAsync("api: merged");

        (await Tools().MergePullRequestAsync("SCRUM-44", how, "CI is green")).Should().Be("api: merged");
    }

    [Fact]
    public async Task MergePullRequest_AnUnknownWayOrTicket_OrNoRunsControl_ActsOnNothing()
    {
        (await Tools().MergePullRequestAsync("SCRUM-44", "later")).Should().Contain("say now or auto");
        (await Tools().MergePullRequestAsync("SCRUM-99", "now")).Should().Contain("is not a ticket of this series");
        (await Tools(mayStartRuns: false).MergePullRequestAsync("SCRUM-44", "now")).Should().Contain("runs.control");
        _acts.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task MergePullRequest_Final_MergesTheFinalPullRequestPastProtection()
    {
        _host.Setup(h => h.MergePullRequestAsync(Final, new BranchName(Branch), BranchProtection.Bypass, It.IsAny<MergeStyle>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PullRequestCompletion.Merged());

        (await Tools().MergePullRequestAsync("final", "now", "the series check passed")).Should().Contain($"{Final} merged");
    }

    [Fact]
    public async Task MergePullRequest_FinalThatConflicts_SaysSoAndRefreshesNothingByItself()
    {
        _host.Setup(h => h.MergePullRequestAsync(Final, It.IsAny<BranchName>(), BranchProtection.Respect, It.IsAny<MergeStyle>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PullRequestCompletion.Refused("conflicts") with { Conflicts = true });

        (await Tools().MergePullRequestAsync("final", "auto")).Should().Contain("refresh_branch brings");
        _launch.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RefreshBranch_ATicketOrTheFinal_StartsTheRefresh()
    {
        _acts.Setup(a => a.RefreshAsync(It.IsAny<SeriesRunRecord>(), "43", It.IsAny<CancellationToken>())).ReturnsAsync("refreshing");
        _launch.Setup(l => l.RefreshAsync("sample", "api", Branch, Final, "90", It.IsAny<CancellationToken>())).ReturnsAsync("final refreshing");

        (await Tools().RefreshBranchAsync("SCRUM-43", "it conflicts")).Should().Be("refreshing");
        (await Tools().RefreshBranchAsync("final")).Should().Be("final refreshing");
    }

    [Fact]
    public async Task RetargetPullRequest_ReachesTheRetargetAct()
    {
        _acts.Setup(a => a.RetargetAsync(It.IsAny<SeriesRunRecord>(), "44", "api", "main", It.IsAny<CancellationToken>())).ReturnsAsync("moved");

        (await Tools().RetargetPullRequestAsync("SCRUM-44", "api", "main", "its prerequisite landed")).Should().Be("moved");
    }

    [Fact]
    public async Task Ci_AReadNeedsNoRunsControl_ARequeueDoes_AnUnknownActIsSaid()
    {
        _acts.Setup(a => a.CiAsync(It.IsAny<SeriesRunRecord>(), "43", SeriesCiActs.Read, It.IsAny<CancellationToken>())).ReturnsAsync("CI green");

        (await Tools(mayStartRuns: false).CiAsync("SCRUM-43", "Read")).Should().Be("CI green");
        (await Tools(mayStartRuns: false).CiAsync("SCRUM-43", "requeue")).Should().Contain("runs.control");
        (await Tools().CiAsync("SCRUM-43", "wait")).Should().Contain("is no CI act");
        _acts.Verify(a => a.CiAsync(It.IsAny<SeriesRunRecord>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Ci_FinalRead_AsksTheHostForTheHeadsCi()
    {
        _host.As<IPullRequestCiReader>().Setup(c => c.ReadCiAsync(Final, It.IsAny<CiReadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PullRequestCi(CiStates.Red, "abcdef123", null, [new CiFailure("build", "https://ci", "boom\nmore")]));

        (await Tools().CiAsync("final", "read")).Should().Be($"api ({Final}): CI red on head abcdef1 — build: boom.");
    }
}
