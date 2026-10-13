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

    private async Task RunAsync(DateTimeOffset now, OpenPullRequestsPage page)
    {
        _page = page;
        using var ctx = _store.Context();
        var store = new PrSweepStore(ctx, new SqliteUniqueViolationTranslator());
        var admission = new PrCommentCommandAdmission(new CommentIntentParser(_intents.Object), new ServerContext("c.yml"),
            _commands.Object, Mock.Of<IPrTicketOwner>(), Mock.Of<IReworkNudges>(), NullLogger<PrCommentCommandAdmission>.Instance);
        var trust = new Mock<IPrCommentAuthorTrust>();
        trust.Setup(t => t.IsTrustedAsync(It.IsAny<PrCommentAuthor>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var keyed = new ServiceCollection().AddKeyedSingleton("github", trust.Object).BuildServiceProvider();
        var actions = new PrSweepActions(new PrTriggerLabelResolver(), new PrRunContextFactory(), _launcher.Object, admission, keyed);
        await new PrSweepPass(store, actions, new PrTriggerLabelResolver()).RunAsync(Target(), _lister.Object, null, Interval, now, 1, CancellationToken.None);
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
}
