using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Dialogue;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Domain.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.ChatLaunch;
using AgentSmith.Server.Services.Handlers;
using AgentSmith.Server.Services.Init;
using AgentSmith.Server.Services.Lifecycle;
using AgentSmith.Server.Services.Webhooks;
using AgentSmith.Tests.Spawning;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Server;

/// <summary>
/// The chat doors that start a run with no ticket — init and security review — against the
/// production run repository over a real SQLite store. Only the job queue, the capacity probe
/// and the pull-request host are doubles: that is where the process boundary is.
/// </summary>
public sealed class ChatTicketlessRunLaunchTests : IDisposable
{
    private const string Project = "sample";
    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly List<PipelineRequest> _enqueued = [];
    private readonly ChatRunHarness _chat = new();
    private readonly Mock<IPrDiffProviderFactory> _diffs = new();
    private IReadOnlyList<RepoConnection> _repos = [Repo("sample-server")];
    private ISandboxCapacityProbe _probe = CapacityTestDoubles.AlwaysAdmit();

    private IEnumerable<string> _said => _chat.Slack.Posts.Select(p => p.Text);

    public void Dispose()
    {
        _chat.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task InitProjectIntent_LaunchesThroughInitRunLauncher()
    {
        await InitHandler().HandleAsync(InitIntent(), CancellationToken.None);

        var request = _enqueued.Should().ContainSingle().Subject;
        request.PipelineName.Should().Be(InitRunLauncher.PipelineName);
        request.IsInit.Should().BeTrue();
        request.Context.Should().Contain(ContextKeys.RefreshPrinciples, false,
            "a chat command never refreshes principles — that is the launching click's word");
        RunRow(request.RunId!).Status.Should().Be("queued");
        _said.Should().ContainSingle().Which.Should().Contain(request.RunId!);
    }

    [Fact]
    public async Task ChatSecurityReview_WithoutTicket_IsAdmittedQueuedAndRecorded()
    {
        await SecurityHandler().HandleAsync(Review(pr: null), CancellationToken.None);

        var request = _enqueued.Should().ContainSingle().Subject;
        request.PipelineName.Should().Be("security-scan");
        request.TicketId.Should().BeNull("a chat security review has no ticket and fabricates none");
        request.IsInit.Should().BeFalse();
        var run = RunRow(request.RunId!);
        run.Status.Should().Be("queued");
        run.Trigger.Should().Be(InitRunRepository.ManualTrigger);
        run.Pipeline.Should().Be("security-scan");
        _said.Should().ContainSingle().Which.Should().Contain(request.RunId!);
    }

    [Fact]
    public async Task ChatSecurityReview_WithPr_SeedsPrContext()
    {
        _diffs.Setup(f => f.Create(It.IsAny<RepoConnection>())).Returns(Diff(new PrDiff(
            "base-sha", "head-sha", [], HeadBranch: "feature/login", Author: "octo")));

        await SecurityHandler().HandleAsync(Review(pr: "42"), CancellationToken.None);

        var context = _enqueued.Should().ContainSingle().Subject.Context!;
        context[ContextKeys.PrNumber].Should().Be("42");
        context[ContextKeys.CheckoutBranch].Should().Be("feature/login");
        context[ContextKeys.PrHead].Should().Be("head-sha");
        context[ContextKeys.PrBase].Should().Be("base-sha");
        context[ContextKeys.PrAuthor].Should().Be("octo");
        context[ContextKeys.SourceOverrideRepo].Should().Be("sample-server");
    }

    [Fact]
    public async Task ChatSecurityReview_WithPrInAProjectOfSeveralRepos_IsRefusedAndStartsNothing()
    {
        _repos = [Repo("sample-server"), Repo("sample-client")];

        await SecurityHandler().HandleAsync(Review(pr: "42"), CancellationToken.None);

        _enqueued.Should().BeEmpty();
        _said.Should().ContainSingle().Which.Should().Contain("which repo");
    }

    [Fact]
    public async Task ChatSecurityReview_NoCapacity_IsRefusedWithTheReason()
    {
        _probe = CapacityTestDoubles.AlwaysDeny("namespace quota full");

        await SecurityHandler().HandleAsync(Review(pr: null), CancellationToken.None);

        _enqueued.Should().BeEmpty();
        _said.Should().ContainSingle().Which.Should().Contain("namespace quota full");
    }

    // 2026-10-08-e8b9e: a ticketless PR command ('/as review') launches through the same chat launcher,
    // with the pull request's context and the admission's pipeline — no second parse, no ticket.
    [Fact]
    public async Task Admission_TicketlessReview_LaunchesWithoutTicket()
    {
        var comments = new Mock<IPrCommentProvider>();

        await TicketlessLaunch(comments, Host(principles: true)).LaunchAsync(
            Command(sameRepository: false), new PipelineRequest(Project, "security-scan", TicketId: null, Headless: true), CancellationToken.None);

        var request = _enqueued.Should().ContainSingle().Subject;
        request.TicketId.Should().BeNull();
        request.PipelineName.Should().Be("security-scan");
        request.Context![ContextKeys.PrNumber].Should().Be("42");
        comments.Verify(c => c.PostCommentAsync("42", It.Is<string>(t => t.Contains(request.RunId!)), It.IsAny<CancellationToken>()), Times.Once);
    }

    // 2026-10-13-0b82b: the branch is read before a run exists; one answer, no run.
    [Fact]
    public async Task PrCommand_UninitialisedBranch_AnswersOnceAndStartsNothing()
    {
        var comments = new Mock<IPrCommentProvider>();

        await TicketlessLaunch(comments, Host(principles: false)).LaunchAsync(
            Command(sameRepository: true), new PipelineRequest(Project, "pr-review", TicketId: null, Headless: true), CancellationToken.None);

        _enqueued.Should().BeEmpty();
        comments.Verify(c => c.PostCommentAsync("42", It.Is<string>(t => t.Contains("branch 'feature/login' is not initialised")
            && t.Contains("'api' has no principles.md") && t.Contains("init-project")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PrCommand_ProbeUnknown_StillLaunches()
    {
        var host = Host(principles: false);
        host.Setup(p => p.BranchExistsAsync(It.IsAny<BranchName>(), It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("503"));

        await TicketlessLaunch(new Mock<IPrCommentProvider>(), host).LaunchAsync(
            Command(sameRepository: true), new PipelineRequest(Project, "pr-review", TicketId: null, Headless: true), CancellationToken.None);

        _enqueued.Should().ContainSingle();
    }

    private PrCommandLaunch TicketlessLaunch(Mock<IPrCommentProvider> comments, Mock<ISourceProvider> host)
    {
        _diffs.Setup(f => f.Create(It.IsAny<RepoConnection>())).Returns(Diff(new PrDiff(
            "base-sha", "head-sha", [], HeadBranch: "feature/login", Author: "octo")));
        var hosts = new Mock<ISourceProviderFactory>();
        hosts.Setup(f => f.Create(It.IsAny<RepoConnection>())).Returns(host.Object);
        var contexts = new Mock<ISandboxLanguageResolver>();
        contexts.Setup(c => c.ListContextsAsync(It.IsAny<RepoConnection>(), It.IsAny<ContextReadBranches>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RemoteContextListing([new RemoteContextDiscovery("api", ".", "csharp")]));
        var services = new ServiceCollection();
        services.AddSingleton(new ChatPrContextResolver(_diffs.Object, new PrRunContextFactory()));
        services.AddSingleton(new ChatTicketlessRunLauncher(Runs(), Admission(), Dispatch(), TimeProvider.System,
            NullLogger<ChatTicketlessRunLauncher>.Instance));
        services.AddSingleton(new PrCommandBootstrapCheck(new RemoteBootstrapProbe(hosts.Object, contexts.Object,
            NullLogger<RemoteBootstrapProbe>.Instance)));
        var sources = new Mock<ISourceProviderFactory>();
        sources.Setup(f => f.Create(It.IsAny<RepoConnection>())).Returns(comments.As<ISourceProvider>().Object);
        return new PrCommandLaunch(ConfigLoader(), new ServerContext("agentsmith.yml"),
            new AgentSmith.Infrastructure.Services.Webhooks.ConfiguredRepoFinder(), Mock.Of<IActiveRunLease>(),
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            sources.Object, NullLogger<PrCommandLaunch>.Instance);
    }

    private static Mock<ISourceProvider> Host(bool principles)
    {
        var host = new Mock<ISourceProvider>();
        host.Setup(p => p.BranchExistsAsync(It.IsAny<BranchName>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        host.Setup(p => p.TryReadFileAsync(It.IsAny<string>(), It.IsAny<BranchName>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string path, BranchName _, CancellationToken _) =>
                path.EndsWith("/principles.md") && !principles ? null : "content");
        return host;
    }

    private static PrCommentCommand Command(bool sameRepository) =>
        new("/as review", new AgentSmith.Contracts.Webhooks.PrCommentAuthor(
            "https://github.com/o/sample-server", "o/sample-server", "octo", "octo"), "o/sample-server#42", "pr:o/sample-server#42", "42")
        { SameRepository = sameRepository, HeadRef = "feature/login" };

    private SecurityReviewIntentHandler SecurityHandler() => new(
        ConfigLoader(), new ServerContext("agentsmith.yml"),
        new ChatPrContextResolver(_diffs.Object, new PrRunContextFactory()),
        new ChatTicketlessRunLauncher(
            Runs(), Admission(), Dispatch(), TimeProvider.System,
            NullLogger<ChatTicketlessRunLauncher>.Instance),
        _chat.Get<ChatRunStart>());

    private InitProjectIntentHandler InitHandler() => new(
        new InitRunLauncher(
            ConfigLoader(), new ServerContext("agentsmith.yml"), Runs(), Admission(), Dispatch(),
            AcquiringLock(), TimeProvider.System, NullLogger<InitRunLauncher>.Instance),
        _chat.Get<ChatRunStart>());

    private static IRedisClaimLock AcquiringLock()
    {
        var claimLock = new Mock<IRedisClaimLock>();
        claimLock.Setup(l => l.TryAcquireAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("token");
        return claimLock.Object;
    }

    private InitRunRepository Runs() => new(new AgentSmithDbContext(Options()), TimeProvider.System);

    private InitRunAdmission Admission() => new(
        CapacityTestDoubles.StubCalculator(), CapacityTestDoubles.AlwaysReserve(),
        CapacityTestDoubles.NoHolds(), _probe, NullLogger<InitRunAdmission>.Instance);

    private QueuedRunDispatch Dispatch() => TestQueuedRunDispatch.Over(() => new AgentSmithDbContext(Options()), Queue());

    private IRedisJobQueue Queue()
    {
        var queue = new Mock<IRedisJobQueue>();
        queue.Setup(q => q.EnqueueAsync(It.IsAny<PipelineRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PipelineRequest, CancellationToken>((r, _) => _enqueued.Add(r))
            .Returns(Task.CompletedTask);
        return queue.Object;
    }

    private IConfigurationLoader ConfigLoader()
    {
        var config = new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject>
            {
                [Project] = new() { Name = Project, Repos = _repos },
            },
        };
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(config);
        return loader.Object;
    }

    private static IPrDiffProvider Diff(PrDiff diff)
    {
        var provider = new Mock<IPrDiffProvider>();
        provider.Setup(p => p.GetDiffAsync("42", It.IsAny<CancellationToken>())).ReturnsAsync(diff);
        return provider.Object;
    }

    private static RepoConnection Repo(string name) =>
        new() { Name = name, Type = RepoType.GitHub, Url = $"https://github.com/o/{name}" };

    private static SecurityReviewIntent Review(string? pr) => new()
    {
        RawText = "security-review", UserId = "U1", ChannelId = "C1", Platform = "slack",
        Project = Project, PrIdentifier = pr,
    };

    private static InitProjectIntent InitIntent() => new()
    {
        RawText = "init sample", UserId = "U1", ChannelId = "C1", Platform = "slack", Project = Project,
    };

    private AgentSmith.Infrastructure.Persistence.Entities.Run RunRow(string runId)
    {
        using var ctx = new AgentSmithDbContext(Options());
        return ctx.Runs.AsNoTracking().Single(r => r.Id == runId);
    }

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;
}
