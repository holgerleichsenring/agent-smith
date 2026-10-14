using AgentSmith.Application.Webhooks;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Sweep;
using AgentSmith.Contracts.Webhooks;
using AgentSmith.Infrastructure.Persistence.Entities;
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

/// <summary>2026-10-08-10b0: one repository's pull-request events — record first, then each start once.
/// 2026-10-13-0b82a: opening or pushing starts nothing; a label and a command do.</summary>
public sealed class PrSweepPassTests : IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
    private readonly ServerStateStore _store = new();
    private readonly Mock<IDetachedPipelineLauncher> _launcher = new();
    private readonly Mock<IOpenPullRequestLister> _lister = new();
    private readonly Mock<IPrCommandLaunch> _commands = new();
    private readonly Mock<IIntentParser> _intents = new();
    private OpenPullRequestsPage _page = new([]);
    private IReadOnlyList<PrSweepComment> _comments = [];

    public PrSweepPassTests()
    {
        _lister.Setup(l => l.ListOpenAsync(It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => _page);
        _lister.Setup(l => l.CommentsSinceAsync(It.IsAny<IReadOnlyList<OpenPullRequest>>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _comments);
        _commands.Setup(l => l.LaunchAsync(It.IsAny<PrCommentCommand>(), It.IsAny<PipelineRequest>(), It.IsAny<CancellationToken>(),
            It.IsAny<IReadOnlySet<string>?>())).ReturnsAsync(WebhookResult.HandledNoRoute());
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

    private readonly Mock<IPrTicketOwner> _owners = new();
    private readonly List<ReworkNudgeRequest> _nudged = [];

    private IReworkNudges Nudges()
    {
        var nudges = new Mock<IReworkNudges>();
        nudges.Setup(n => n.EnqueueAsync(It.IsAny<ReworkNudgeRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ReworkNudgeRequest, CancellationToken>((r, _) => _nudged.Add(r)).Returns(Task.CompletedTask);
        return nudges.Object;
    }

    private async Task RunAsync(DateTimeOffset now, OpenPullRequestsPage page, string? resume = null)
    {
        _page = page;
        using var ctx = _store.Context();
        var store = new PrSweepStore(ctx, new SqliteUniqueViolationTranslator());
        var admission = new PrCommentCommandAdmission(new CommentIntentParser(_intents.Object), new ServerContext("c.yml"),
            _commands.Object, _owners.Object, Nudges(), AgentSmith.Tests.Webhooks.PrRecordNudges.Of(_owners.Object, Nudges()),
            NullLogger<PrCommentCommandAdmission>.Instance);
        var trust = new Mock<IPrCommentAuthorTrust>();
        trust.Setup(t => t.IsTrustedAsync(It.IsAny<PrCommentAuthor>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var keyed = new ServiceCollection().AddKeyedSingleton("github", trust.Object).BuildServiceProvider();
        var actions = new PrSweepActions(new PrTriggerLabelResolver(), new PrRunContextFactory(), _launcher.Object, admission, keyed);
        using var scope = _store.ScopeFactory.CreateScope();
        var records = new PrSweepRecordNudge(scope.ServiceProvider.GetRequiredService<PrReviewRecordRepository>(), Nudges());
        await new PrSweepPass(store, actions, new PrTriggerLabelResolver(), records)
            .RunAsync(Target(), _lister.Object, resume, Interval, now, 1, CancellationToken.None);
    }

    private void VerifyLaunched(string pipeline, Times times) =>
        times.Validate(_launcher.Invocations.Count(i => i.Method.Name == nameof(IDetachedPipelineLauncher.LaunchAsync)
            && (string)i.Arguments[0] == "p" && (string)i.Arguments[1] == pipeline)).Should().BeTrue();

    [Fact]
    public async Task PrSweep_FirstSighting_RecordsOnly()
    {
        await CycleAsync(Noon, Pr("4", "h1", "scan-me"));

        _launcher.VerifyNoOtherCalls();
    }

    // 2026-10-13-0b82a: opening a pull request is a change, not an assignment.
    [Fact]
    public async Task PrSweep_NewPullRequest_StartsNothing()
    {
        await CycleAsync(Noon, Pr("4", "h1"));
        await CycleAsync(Noon.AddMinutes(1), Pr("4", "h1"), Pr("5", "h9"));

        _launcher.VerifyNoOtherCalls();
        _commands.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task PrSweep_HeadMoved_StartsNothing()
    {
        await CycleAsync(Noon, Pr("4", "h1"));
        await CycleAsync(Noon.AddMinutes(1), Pr("4", "h2"));
        await CycleAsync(Noon.AddMinutes(2), Pr("4", "h3"));

        _launcher.VerifyNoOtherCalls();
        _commands.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task PrSweep_NewPullRequestWithLabel_StillScans()
    {
        await CycleAsync(Noon, Pr("4", "h1"));
        await CycleAsync(Noon.AddMinutes(1), Pr("4", "h1"), Pr("5", "h9", "scan-me"));

        VerifyLaunched("security-scan", Times.Once());
        VerifyLaunched("pr-review", Times.Never());
    }

    [Fact]
    public async Task PrSweep_TrustedCommand_StartsPrReview()
    {
        await CycleAsync(Noon, Pr("4", "h1"));
        _comments = [new PrSweepComment("4", "c1", Noon.AddSeconds(30), "/agent-smith pr-review",
            new PrCommentAuthor("https://github.com/o/r", "o/r", "bob", "bob"))];
        await CycleAsync(Noon.AddMinutes(1), Pr("4", "h1"));
        await CycleAsync(Noon.AddMinutes(2), Pr("4", "h1"));

        _commands.Verify(l => l.LaunchAsync(It.Is<PrCommentCommand>(c => c.PrNumber == "4"),
            It.Is<PipelineRequest>(r => r.PipelineName == "pr-review"), It.IsAny<CancellationToken>(), It.IsAny<IReadOnlySet<string>?>()),
            Times.Once());
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

        var first = await store.TrySetLabelAsync("github.com/o/r", "4", false, true, CancellationToken.None);
        var second = await store.TrySetLabelAsync("github.com/o/r", "4", false, true, CancellationToken.None);

        first.Should().BeTrue();
        second.Should().BeFalse("a cycle that read the old label flag loses the write and launches nothing");
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

    private async Task RecordAsync(string number, string state)
    {
        await using var ctx = _store.Context();
        var url = $"https://github.com/o/r/pull/{number}";
        ctx.Add(new PrReviewRecord
        {
            PrKey = PrKey.Of(url), Url = url, Repo = "r", RepoKey = RepoKey.Of(Repo.Url!), Number = number,
            Project = "p", TicketId = "12", RunId = "run-1", State = state,
        });
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task PrSweep_RecordOpenButNotListed_NudgesRefresh_NeverRecordedPrToo()
    {
        await RecordAsync("4", PrReviewStates.Open);
        await RecordAsync("8", PrReviewStates.Open);

        // 8 was merged before any sweep listed it open: it has no sweep row, only its record.
        await CycleAsync(Noon, Pr("4", "h1"));

        _nudged.Should().ContainSingle().Which.Should().Match<ReworkNudgeRequest>(n => n.Project == "p" && n.TicketId == "12"
            && n.PrUrl == "https://github.com/o/r/pull/8" && n.Origin == ReworkNudgeOrigin.Refresh && n.IsLate);
    }

    [Fact]
    public async Task PrSweep_ResumedList_NudgesNoOpenRecord()
    {
        await RecordAsync("8", PrReviewStates.Open);

        await RunAsync(Noon, new OpenPullRequestsPage([Pr("4", "h1")]), resume: "2");
        await RunAsync(Noon.AddMinutes(1), new OpenPullRequestsPage([Pr("4", "h1")], Cut: true, Resume: "2"));

        _nudged.Should().BeEmpty("a list that is cut or resumed does not show what is no longer open");
    }

    [Fact]
    public async Task PrSweep_ReopenedPr_NudgesRefreshWithItsUrl()
    {
        await RecordAsync("4", PrReviewStates.Merged);
        await RecordAsync("5", PrReviewStates.Closed);

        await RunAsync(Noon, new OpenPullRequestsPage([Pr("5", "h1")], Cut: true, Resume: "2"));

        _nudged.Select(n => n.PrUrl).Should().Equal("https://github.com/o/r/pull/5");
    }

    [Fact]
    public async Task PrSweep_PlainComment_NudgesRefresh()
    {
        _owners.Setup(o => o.FindAsync(It.IsAny<string>(), "12", It.IsAny<IReadOnlySet<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConfiguredRepo("p", new ResolvedProject(), Repo));
        var pr = Pr("4", "h1") with { HeadBranch = "agent-smith/12", SameRepository = true };
        await CycleAsync(Noon, pr);
        _comments = [new PrSweepComment("4", "c1", Noon.AddSeconds(30), "looks good", new PrCommentAuthor("https://github.com/o/r", "o/r", "bob", "bob"))];

        await CycleAsync(Noon.AddMinutes(1), pr);

        _nudged.Should().ContainSingle().Which.Should().Match<ReworkNudgeRequest>(n => n.Origin == ReworkNudgeOrigin.Refresh
            && n.PrUrl == "https://github.com/o/r/pull/4" && !n.FromDelivery);
        _commands.VerifyNoOtherCalls();
    }
}
