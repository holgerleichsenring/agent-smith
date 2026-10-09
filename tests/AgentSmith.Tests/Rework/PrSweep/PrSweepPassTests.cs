using AgentSmith.Application.Webhooks;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Sweep;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services.Translators;
using AgentSmith.Infrastructure.Services.Webhooks;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Services.Sweep;
using AgentSmith.Server.Services.Webhooks;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Rework.PrSweep;

/// <summary>2026-10-08-10b0: one repository's pull-request events — record first, then each start once.</summary>
public sealed class PrSweepPassTests : IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
    private readonly ServerStateStore _store = new();
    private readonly Mock<IDetachedPipelineLauncher> _launcher = new();
    private readonly Mock<IOpenPullRequestLister> _lister = new();
    private OpenPullRequestsPage _page = new([]);

    public PrSweepPassTests()
    {
        _lister.Setup(l => l.ListOpenAsync(It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => _page);
        _lister.Setup(l => l.CommentsSinceAsync(It.IsAny<IReadOnlyList<OpenPullRequest>>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
    }

    public void Dispose() => _store.Dispose();

    private static readonly RepoConnection Repo = new() { Name = "r", Url = "https://github.com/o/r", Type = RepoType.GitHub };

    private static SweepTarget Target() => new(new AgentSmithConfig
    {
        Projects = new Dictionary<string, ResolvedProject>
        {
            ["p"] = new() { Name = "p", Repos = [Repo], GithubTrigger = new WebhookTriggerConfig { PrTriggerLabel = "scan-me" } },
        },
    }, Repo, new HashSet<string> { "p" });

    private static OpenPullRequest Pr(string number, string head, params string[] labels) =>
        new(number, $"https://github.com/o/r/pull/{number}", head, "base", "feature", "alice", labels, Noon.AddHours(-1), Noon);

    private Task CycleAsync(DateTimeOffset now, params OpenPullRequest[] prs) => RunAsync(now, new OpenPullRequestsPage(prs));

    private async Task RunAsync(DateTimeOffset now, OpenPullRequestsPage page)
    {
        _page = page;
        using var ctx = _store.Context();
        var store = new PrSweepStore(ctx, new SqliteUniqueViolationTranslator());
        var admission = new PrCommentCommandAdmission(new CommentIntentParser(Mock.Of<IIntentParser>()), new ServerContext("c.yml"),
            Mock.Of<IPrCommandLaunch>(), NullLogger<PrCommentCommandAdmission>.Instance);
        var actions = new PrSweepActions(new PrReviewRouteResolver(new ConfiguredRepoFinder()), new PrTriggerLabelResolver(),
            new PrRunContextFactory(), _launcher.Object, admission, Mock.Of<IServiceProvider>());
        await new PrSweepPass(store, actions, new PrTriggerLabelResolver()).RunAsync(Target(), _lister.Object, null, Interval, now, 1, CancellationToken.None);
    }

    private void VerifyLaunched(string pipeline, Times times) =>
        _launcher.Verify(l => l.LaunchAsync("p", pipeline, It.IsAny<Dictionary<string, object>?>()), times);

    [Fact]
    public async Task PrSweep_FirstSighting_RecordsOnly()
    {
        await CycleAsync(Noon, Pr("4", "h1", "scan-me"));

        _launcher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task PrSweep_NewPrAfterInit_Reviewed()
    {
        await CycleAsync(Noon, Pr("4", "h1"));
        await CycleAsync(Noon.AddMinutes(1), Pr("4", "h1"), Pr("5", "h9"));

        VerifyLaunched("pr-review", Times.Once());
    }

    [Fact]
    public async Task PrSweep_ModeSwitchBack_NoReplay()
    {
        await CycleAsync(Noon, Pr("4", "h1"));
        await CycleAsync(Noon.AddHours(5), Pr("4", "h2"), Pr("5", "h9"));

        _launcher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task PrSweep_NewHead_StartsPrReviewOnce()
    {
        await CycleAsync(Noon, Pr("4", "h1"));
        await CycleAsync(Noon.AddMinutes(1), Pr("4", "h2"));
        await CycleAsync(Noon.AddMinutes(2), Pr("4", "h2"));

        VerifyLaunched("pr-review", Times.Once());
    }

    [Fact]
    public async Task PrSweep_LabelAdded_StartsScanOnce()
    {
        await CycleAsync(Noon, Pr("4", "h1"));
        await CycleAsync(Noon.AddMinutes(1), Pr("4", "h1", "scan-me"));
        await CycleAsync(Noon.AddMinutes(2), Pr("4", "h1", "scan-me"));

        VerifyLaunched("security-scan", Times.Once());
    }

    [Fact]
    public async Task PrSweep_ConcurrentCycles_CompareAndSetLaunchesOnce()
    {
        await CycleAsync(Noon, Pr("4", "h1"));
        using var ctx = _store.Context();
        var store = new PrSweepStore(ctx, new SqliteUniqueViolationTranslator());

        var first = await store.TryMoveHeadAsync("github.com/o/r", "4", "h1", "h2", CancellationToken.None);
        var second = await store.TryMoveHeadAsync("github.com/o/r", "4", "h1", "h2", CancellationToken.None);

        first.Should().BeTrue();
        second.Should().BeFalse("a cycle that read the old head loses the write and launches nothing");
    }

    [Fact]
    public async Task PrSweep_MissingFromCappedList_NotPruned()
    {
        await CycleAsync(Noon, Pr("4", "h1"), Pr("5", "h5"));
        await RunAsync(Noon.AddMinutes(1), new OpenPullRequestsPage([Pr("4", "h1")], Cut: true, Resume: "2"));
        await CycleAsync(Noon.AddMinutes(2), Pr("4", "h1"));

        using var read = _store.Context();
        var states = await new PrSweepStore(read, new SqliteUniqueViolationTranslator()).StatesAsync("github.com/o/r", CancellationToken.None);
        states.Keys.Should().BeEquivalentTo(["4"], "the cut list pruned nothing; the complete one pruned the closed pull request");
    }
}
