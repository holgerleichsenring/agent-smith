using AgentSmith.Application.Services.Dialogue;
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
using AgentSmith.Server.Services.Webhooks;
using AgentSmith.Tests.Spawning;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
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
    private readonly List<string> _said = [];
    private readonly Mock<IPrDiffProviderFactory> _diffs = new();
    private IReadOnlyList<RepoConnection> _repos = [Repo("sample-server")];
    private ISandboxCapacityProbe _probe = CapacityTestDoubles.AlwaysAdmit();

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task InitProjectIntent_LaunchesThroughInitRunLauncher()
    {
        await InitHandler().HandleAsync(InitIntent(), CancellationToken.None);

        var request = _enqueued.Should().ContainSingle().Subject;
        request.PipelineName.Should().Be(InitRunLauncher.PipelineName);
        request.IsInit.Should().BeTrue();
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

    private SecurityReviewIntentHandler SecurityHandler() => new(
        ConfigLoader(), new ServerContext("agentsmith.yml"),
        new ChatPrContextResolver(_diffs.Object, new PrRunContextFactory()),
        new ChatTicketlessRunLauncher(
            Runs(), Admission(), Queue(), TimeProvider.System,
            NullLogger<ChatTicketlessRunLauncher>.Instance),
        Announcer());

    private InitProjectIntentHandler InitHandler() => new(
        new InitRunLauncher(
            ConfigLoader(), new ServerContext("agentsmith.yml"), Runs(), Admission(), Queue(),
            TimeProvider.System, NullLogger<InitRunLauncher>.Instance),
        Announcer());

    private ChatLaunchAnnouncer Announcer()
    {
        var adapter = new Mock<IPlatformAdapter>();
        adapter.Setup(a => a.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, text, _) => _said.Add(text))
            .Returns(Task.CompletedTask);
        return new ChatLaunchAnnouncer(adapter.Object, new RunAnswerLink(new AgentSmithConfig()));
    }

    private InitRunRepository Runs() => new(new AgentSmithDbContext(Options()), TimeProvider.System);

    private InitRunAdmission Admission() => new(
        CapacityTestDoubles.StubCalculator(), CapacityTestDoubles.AlwaysReserve(),
        CapacityTestDoubles.NoHolds(), _probe, NullLogger<InitRunAdmission>.Instance);

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
