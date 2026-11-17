using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.Branches;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Branches;

/// <summary>2026-11-17-713ec: the server starts a project's branch-cleanup run as a run — a queued row and a
/// dispatched request — and leaves a project with nothing to sweep, or with a sweep still live, alone.</summary>
public sealed class WorkBranchCleanupLauncherTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly List<PipelineRequest> _enqueued = [];
    private Action _onEnqueued = () => { };

    public WorkBranchCleanupLauncherTests()
    {
        _connection.Open();
        using var ctx = new AgentSmithDbContext(Options());
        MigratedStoreTemplate.CopyInto(ctx);
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Launcher_AHostedProject_GetsAQueuedRowAndADispatchedRequest()
    {
        var runId = await Launcher().LaunchAsync(Project(RepoType.GitHub), CancellationToken.None);

        runId.Should().NotBeNull();
        var request = _enqueued.Should().ContainSingle().Subject;
        request.PipelineName.Should().Be(PipelinePresets.BranchCleanupName);
        request.TicketId.Should().BeNull();
        using var ctx = new AgentSmithDbContext(Options());
        var row = ctx.Runs.Single(r => r.Id == runId);
        row.Status.Should().Be("queued");
        row.Pipeline.Should().Be(PipelinePresets.BranchCleanupName);
    }

    [Fact]
    public async Task Launcher_ASweepStillLive_StartsNoSecond()
    {
        await Launcher().LaunchAsync(Project(RepoType.GitHub), CancellationToken.None);

        (await Launcher().LaunchAsync(Project(RepoType.GitHub), CancellationToken.None)).Should().BeNull();
        _enqueued.Should().ContainSingle();
    }

    [Fact]
    public async Task Launcher_OnlyLocalRepositories_HasNothingToSweep()
    {
        (await Launcher().LaunchAsync(Project(RepoType.Local), CancellationToken.None)).Should().BeNull();
        _enqueued.Should().BeEmpty();
    }

    [Fact]
    public async Task HostedPasses_Disabled_LaunchesNothing()
    {
        using var cts = new CancellationTokenSource();
        var services = new ServiceCollection().AddScoped(_ => Launcher()).BuildServiceProvider();
        var service = new WorkBranchCleanupHostedService(services, new ServerContext("c.yml"),
            Mock.Of<IConfigurationLoader>(), NullLogger<WorkBranchCleanupHostedService>.Instance);
        var config = new AgentSmithConfig
        {
            BranchCleanup = new BranchCleanupConfig { Enabled = false },
            Projects = new Dictionary<string, ResolvedProject> { ["p"] = Project(RepoType.GitHub) },
        };

        var passes = service.PassesAsync(config, cts.Token);
        await cts.CancelAsync();

        await passes.Invoking(p => p).Should().ThrowAsync<OperationCanceledException>("a disabled sweep only waits for the epoch to end");
        _enqueued.Should().BeEmpty();
    }

    [Fact]
    public async Task HostedPasses_Enabled_LaunchesEveryProjectAtOnce()
    {
        using var cts = new CancellationTokenSource();
        _onEnqueued = () => { if (_enqueued.Count == 2) cts.Cancel(); };
        var services = new ServiceCollection().AddScoped(_ => Launcher()).BuildServiceProvider();
        var service = new WorkBranchCleanupHostedService(services, new ServerContext("c.yml"),
            Mock.Of<IConfigurationLoader>(), NullLogger<WorkBranchCleanupHostedService>.Instance);
        var config = new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject>
            {
                ["a"] = Project(RepoType.GitHub, "a"), ["b"] = Project(RepoType.GitLab, "b"),
            },
        };

        var passes = () => service.PassesAsync(config, cts.Token);

        await passes.Should().ThrowAsync<OperationCanceledException>("the epoch ended while waiting for the interval");
        _enqueued.Select(r => r.ProjectName).Should().BeEquivalentTo(["a", "b"]);
    }

    private WorkBranchCleanupLauncher Launcher() => new(
        new InitRunRepository(new AgentSmithDbContext(Options()), TimeProvider.System),
        TestQueuedRunDispatch.Over(() => new AgentSmithDbContext(Options()), Queue()),
        TimeProvider.System, NullLogger<WorkBranchCleanupLauncher>.Instance);

    private IRedisJobQueue Queue()
    {
        var queue = new Mock<IRedisJobQueue>();
        queue.Setup(q => q.EnqueueAsync(It.IsAny<PipelineRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PipelineRequest, CancellationToken>((r, _) => { _enqueued.Add(r); _onEnqueued(); }).Returns(Task.CompletedTask);
        return queue.Object;
    }

    private static ResolvedProject Project(RepoType type, string name = "p") => new()
    {
        Name = name, Repos = [new RepoConnection { Name = "server", Type = type, Url = "https://x/y.git" }],
    };

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;
}
