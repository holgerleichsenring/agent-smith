using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Infrastructure.Persistence.Services.Translators;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Derivation;
using AgentSmith.Server.Services.Init;
using AgentSmith.Tests.Spawning;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Derivation;

/// <summary>
/// 2026-10-26-bdd0e: a spec derivation starts under the ticket's lease, through the production lease, run row and
/// dispatch over a real SQLite store; the job queue, the capacity probe and the attempt reader are the doubles.
/// </summary>
public sealed class SpecDerivationLauncherTests : IDisposable
{
    private const string Project = "sample";
    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly List<PipelineRequest> _enqueued = [];
    private readonly Mock<IPreviousAttemptReader> _attempts = new();
    private ISandboxCapacityProbe _probe = CapacityTestDoubles.AlwaysAdmit();
    private readonly DbActiveRunLease _lease;

    public SpecDerivationLauncherTests() => _lease = new DbActiveRunLease(Scopes());

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task SpecDerivationLauncher_Started_HoldsTheLeaseAndDispatchesSpecDerive()
    {
        var result = await Launcher().LaunchAsync(Project, "#42", preview: true, CancellationToken.None);

        result.Outcome.Should().Be(SpecDerivationOutcome.Started);
        var request = _enqueued.Should().ContainSingle().Subject;
        request.PipelineName.Should().Be(PipelinePresets.SpecDeriveName);
        request.TicketId.Should().Be(new TicketId("42"));
        request.RunId.Should().Be(result.RunId);
        request.Context![ContextKeys.SpecDerivationPreview].Should().Be(true);
        (await _lease.GetByTicketAsync(Project, new TicketId("42"), CancellationToken.None)).Should().NotBeNull(
            "no code run may start while the derivation runs");
        using var db = Db();
        var row = db.Runs.Single();
        (row.Pipeline, row.TicketId, row.Status, row.Platform).Should().Be((PipelinePresets.SpecDeriveName, "42", "queued", "github"));
    }

    [Fact]
    public async Task SpecDerivationLauncher_LeaseHeld_RefusesAndTakesNothing()
    {
        await _lease.TryClaimAsync(Project, new TicketId("42"), CancellationToken.None);

        var result = await Launcher().LaunchAsync(Project, "42", preview: false, CancellationToken.None);

        result.Outcome.Should().Be(SpecDerivationOutcome.Refused);
        result.Reason.Should().Be(SpecDerivationTexts.Held);
        _enqueued.Should().BeEmpty();
        Db().Runs.Should().BeEmpty();
    }

    [Fact]
    public async Task SpecDerivationLauncher_AttemptWaiting_Refuses()
    {
        _attempts.Setup(a => a.LatestAsync(Project, "42", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PreviousAttempt("run-1", RunStatuses.WaitingForInput, DateTimeOffset.UtcNow, Finished: false));

        var result = await Launcher().LaunchAsync(Project, "42", preview: false, CancellationToken.None);

        result.Reason.Should().Be(SpecDerivationTexts.Waiting);
        (await _lease.GetByTicketAsync(Project, new TicketId("42"), CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task SpecDerivationLauncher_AdmissionRefused_ReleasesTheLease()
    {
        _probe = CapacityTestDoubles.AlwaysDeny("namespace quota full");

        var result = await Launcher().LaunchAsync(Project, "42", preview: false, CancellationToken.None);

        result.Outcome.Should().Be(SpecDerivationOutcome.NoCapacity);
        result.Reason.Should().Contain("namespace quota full");
        (await _lease.GetByTicketAsync(Project, new TicketId("42"), CancellationToken.None)).Should().BeNull(
            "a derivation that never started holds nothing");
    }

    [Fact]
    public async Task DeriveSpecsEndpoint_AnswersTheLauncher()
    {
        var started = await TicketDeriveSpecsEndpoints.DeriveAsync(Project, "42", new DeriveSpecsRequest(), Launcher(), CancellationToken.None);
        var refused = await TicketDeriveSpecsEndpoints.DeriveAsync(Project, "42", null, Launcher(), CancellationToken.None);
        var unknown = await TicketDeriveSpecsEndpoints.DeriveAsync("ghost", "42", null, Launcher(), CancellationToken.None);

        started.Should().BeOfType<Ok<DeriveSpecsResponse>>();
        _enqueued.Single().Context![ContextKeys.SpecDerivationPreview].Should().Be(false, "branch is the default");
        refused.Should().BeAssignableTo<IStatusCodeHttpResult>().Which.StatusCode.Should().Be(409);
        unknown.Should().BeAssignableTo<IStatusCodeHttpResult>().Which.StatusCode.Should().Be(400);
    }

    private SpecDerivationLauncher Launcher() => new(
        ConfigLoader(), new ServerContext("agentsmith.yml"), _lease, new SpecDerivationGate(_attempts.Object),
        new InitRunAdmission(CapacityTestDoubles.StubCalculator(), CapacityTestDoubles.AlwaysReserve(),
            CapacityTestDoubles.NoHolds(), _probe, NullLogger<InitRunAdmission>.Instance),
        new DerivationRunRepository(Db(), TimeProvider.System),
        TestQueuedRunDispatch.Over(Db, Queue()), AcquiringLock(), TimeProvider.System,
        NullLogger<SpecDerivationLauncher>.Instance);

    private AgentSmithDbContext Db() =>
        new(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);

    private IServiceScopeFactory Scopes()
    {
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork>(_ => Db());
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IUniqueViolationTranslator>(new SqliteUniqueViolationTranslator());
        services.AddScoped<ActiveRunRepository>();
        services.AddLogging();
        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    private IRedisJobQueue Queue()
    {
        var queue = new Mock<IRedisJobQueue>();
        queue.Setup(q => q.EnqueueAsync(It.IsAny<PipelineRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PipelineRequest, CancellationToken>((r, _) => _enqueued.Add(r)).Returns(Task.CompletedTask);
        return queue.Object;
    }

    private static IRedisClaimLock AcquiringLock()
    {
        var claimLock = new Mock<IRedisClaimLock>();
        claimLock.Setup(l => l.TryAcquireAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("token");
        return claimLock.Object;
    }

    private static IConfigurationLoader ConfigLoader()
    {
        var config = new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject>
            {
                [Project] = new()
                {
                    Name = Project, Repos = [new RepoConnection { Name = "sample-server" }],
                    Tracker = new TrackerConnection { Type = TrackerType.GitHub },
                },
            },
        };
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(config);
        return loader.Object;
    }
}
