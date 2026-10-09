using AgentSmith.Application.Webhooks;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Sweep;
using AgentSmith.Contracts.Webhooks;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services.Translators;
using AgentSmith.Infrastructure.Services.Webhooks;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Services.Sweep;
using AgentSmith.Server.Services.Webhooks;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly Mock<IPrCommentProvider> _prComments = new();
    private readonly Mock<IIntentParser> _intents = new();
    private OpenPullRequestsPage _page = new([]);
    private IReadOnlyList<PrSweepComment> _comments = [];

    /// <summary>2026-10-09-af10: what the launched review ends with; null leaves it running.</summary>
    private bool? _reviewSucceeds;

    public PrSweepPassTests()
    {
        _lister.Setup(l => l.ListOpenAsync(It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => _page);
        _lister.Setup(l => l.CommentsSinceAsync(It.IsAny<IReadOnlyList<OpenPullRequest>>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _comments);
        _launcher.Setup(l => l.LaunchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, object>?>(), It.IsAny<Func<bool, Task>>()))
            .Returns((string _, string _, Dictionary<string, object>? _, Func<bool, Task> finished) =>
                _reviewSucceeds is { } ok ? finished(ok) : Task.CompletedTask);
        _intents.Setup(p => p.ParseToPipelineRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PipelineRequest("p", "pr-review", TicketId: null, Headless: true));
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
        var launch = new Mock<IPrCommandLaunch>();
        launch.Setup(l => l.LaunchAsync(It.IsAny<PrCommentCommand>(), It.IsAny<PipelineRequest>(), It.IsAny<CancellationToken>(),
            It.IsAny<IReadOnlySet<string>?>())).ReturnsAsync(WebhookResult.HandledNoRoute());
        var admission = new PrCommentCommandAdmission(new CommentIntentParser(_intents.Object), new ServerContext("c.yml"),
            launch.Object, NullLogger<PrCommentCommandAdmission>.Instance);
        var trust = new Mock<IPrCommentAuthorTrust>();
        trust.Setup(t => t.IsTrustedAsync(It.IsAny<PrCommentAuthor>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var keyed = new ServiceCollection().AddKeyedSingleton("github", trust.Object).BuildServiceProvider();
        var provider = new Mock<ISourceProvider>();
        provider.As<IPrCommentProvider>().Setup(c => c.PostCommentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string n, string md, CancellationToken ct) => _prComments.Object.PostCommentAsync(n, md, ct));
        var sources = new Mock<ISourceProviderFactory>();
        sources.Setup(f => f.Create(It.IsAny<RepoConnection>())).Returns(provider.Object);
        var breaker = new PrSweepBreaker(_store.ScopeFactory, sources.Object, NullLogger<PrSweepBreaker>.Instance);
        var actions = new PrSweepActions(new PrReviewRouteResolver(new ConfiguredRepoFinder()), new PrTriggerLabelResolver(),
            new PrRunContextFactory(), _launcher.Object, admission, breaker, keyed);
        await new PrSweepPass(store, actions, new PrTriggerLabelResolver()).RunAsync(Target(), _lister.Object, null, Interval, now, 1, CancellationToken.None);
    }

    // Either launch overload: a review carries its outcome callback, a scan does not.
    private void VerifyLaunched(string pipeline, Times times) =>
        times.Validate(_launcher.Invocations.Count(i => i.Method.Name == nameof(IDetachedPipelineLauncher.LaunchAsync)
            && (string)i.Arguments[0] == "p" && (string)i.Arguments[1] == pipeline)).Should().BeTrue();

    private void OurWipCommitAt(string sha) =>
        _lister.Setup(l => l.HeadCommitMessageAsync(sha, It.IsAny<CancellationToken>()))
            .ReturnsAsync("[wip] agent-smith run 0f3c\n\nRun-Id: 0f3c\nPipeline: pr-review\nFailed-Step: BootstrapGate\n");

    private async Task<AgentSmith.Infrastructure.Persistence.Entities.PrSweepState> RowAsync(string number)
    {
        using var read = _store.Context();
        return (await new PrSweepStore(read, new SqliteUniqueViolationTranslator()).StatesAsync("github.com/o/r", CancellationToken.None))[number];
    }

    private void VerifyPausedNoticePosted(Times times) =>
        _prComments.Verify(c => c.PostCommentAsync("4", It.Is<string>(md => md.StartsWith(PrSweepBreaker.PausedMarker)), It.IsAny<CancellationToken>()), times);

    // 2026-10-09-af10: the incident, end to end at the sweep — a failed review whose run pushed a
    // WIP commit onto the PR branch moved the head, and the next cycle reviewed again.
    [Fact]
    public async Task PrSweep_HeadMovedToOurWipCommit_StartsNothing()
    {
        _reviewSucceeds = false;
        await CycleAsync(Noon, Pr("4", "h1"));
        await CycleAsync(Noon.AddMinutes(1), Pr("4", "h2"));
        OurWipCommitAt("w1");
        await CycleAsync(Noon.AddMinutes(2), Pr("4", "w1"));
        await CycleAsync(Noon.AddMinutes(3), Pr("4", "w1"));

        VerifyLaunched("pr-review", Times.Once());
        (await RowAsync("4")).ReviewedHead.Should().Be("w1", "our head is recorded, so it is never new again");
    }

    [Fact]
    public async Task PrSweep_HeadMessageUnreadable_StillReviews()
    {
        _lister.Setup(l => l.HeadCommitMessageAsync("h2", It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("503"));
        await CycleAsync(Noon, Pr("4", "h1"));
        await CycleAsync(Noon.AddMinutes(1), Pr("4", "h2"));

        VerifyLaunched("pr-review", Times.Once());
    }

    [Fact]
    public async Task PrSweep_FailedReviewOnAHead_NotRetriedOnThatHead()
    {
        _reviewSucceeds = false;
        await CycleAsync(Noon, Pr("4", "h1"));
        await CycleAsync(Noon.AddMinutes(1), Pr("4", "h2"));
        await CycleAsync(Noon.AddMinutes(2), Pr("4", "h2"));
        await CycleAsync(Noon.AddMinutes(3), Pr("4", "h2"));

        VerifyLaunched("pr-review", Times.Once());
        (await RowAsync("4")).FailedReviews.Should().Be(1);
    }

    [Fact]
    public async Task PrSweep_ThreeFailedReviews_FourthHeadStartsNothingAndSaysSoOnce()
    {
        _reviewSucceeds = false;
        await CycleAsync(Noon, Pr("4", "h1"));
        await CycleAsync(Noon.AddMinutes(1), Pr("4", "h2"));
        await CycleAsync(Noon.AddMinutes(2), Pr("4", "h3"));
        await CycleAsync(Noon.AddMinutes(3), Pr("4", "h4"));
        await CycleAsync(Noon.AddMinutes(4), Pr("4", "h5"));
        await CycleAsync(Noon.AddMinutes(5), Pr("4", "h6"));

        VerifyLaunched("pr-review", Times.Exactly(3));
        VerifyPausedNoticePosted(Times.Once());
        (await RowAsync("4")).ReviewedHead.Should().Be("h6");
    }

    [Fact]
    public async Task PrSweep_PausedPr_APersonsCommandResumes()
    {
        _reviewSucceeds = false;
        await CycleAsync(Noon, Pr("4", "h1"));
        foreach (var (head, minute) in new[] { ("h2", 1), ("h3", 2), ("h4", 3) })
            await CycleAsync(Noon.AddMinutes(minute), Pr("4", head));
        _comments = [new PrSweepComment("4", "c1", Noon.AddMinutes(4), "/agent-smith pr-review",
            new PrCommentAuthor("https://github.com/o/r", "o/r", "bob", "bob"))];
        await CycleAsync(Noon.AddMinutes(4).AddSeconds(30), Pr("4", "h4"));
        _comments = [];
        await CycleAsync(Noon.AddMinutes(5), Pr("4", "h5"));

        VerifyLaunched("pr-review", Times.Exactly(4));
    }

    [Fact]
    public async Task PrSweep_SucceededReview_ClearsTheFailures()
    {
        _reviewSucceeds = false;
        await CycleAsync(Noon, Pr("4", "h1"));
        await CycleAsync(Noon.AddMinutes(1), Pr("4", "h2"));
        await CycleAsync(Noon.AddMinutes(2), Pr("4", "h3"));
        _reviewSucceeds = true;
        await CycleAsync(Noon.AddMinutes(3), Pr("4", "h4"));

        (await RowAsync("4")).FailedReviews.Should().Be(0);
    }

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
