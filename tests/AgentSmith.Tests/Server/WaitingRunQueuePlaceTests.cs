using AgentSmith.Application.Services.Claim;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Infrastructure.Persistence.Services.Translators;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.Events;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Server;

/// <summary>
/// 2026-09-22-7c41c: a parked run whose relaunch is merely waiting for a slot stops demanding
/// an answer nobody owes. The run status is untouched — it is the relaunch launcher's own gate
/// — so what the surface reads instead is the place the capacity queue already holds for it,
/// keyed by the parked run's OWN reserved id, unioned with the lease the launcher claims
/// before it removes that entry. A park with neither is unchanged and still says it needs you.
/// </summary>
public sealed class WaitingRunQueuePlaceTests : IDisposable
{
    private const string Project = "p1";
    private readonly SqliteConnection _connection;
    private readonly ICapacityQueue _queue;

    public WaitingRunQueuePlaceTests()
    {
        _connection = MigratedStoreTemplate.OpenCopy();
        _queue = new DbCapacityQueue(ScopeFactory());
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task RunSnapshot_AWaitingRunWithAQueueEntry_CarriesItsPosition()
    {
        await AddRunAsync("run-parked", "waiting_for_input", ticketId: "42");
        await EnqueueResumeAsync("42", "run-parked");

        var (active, _) = await BuildOverviewAsync();

        active.Single(s => s.RunId == "run-parked").QueuePosition.Should().Be(
            1, "the relaunch is enqueued under the parked run's own id, so the place is its place");
    }

    [Fact]
    public async Task RunSnapshot_AWaitingRunWithNoQueueEntry_CarriesNoPosition()
    {
        await AddRunAsync("run-parked", "waiting_for_input", ticketId: "42");

        var (active, _) = await BuildOverviewAsync();

        active.Single().QueuePosition.Should().BeNull(
            "a park nothing is relaunching still owes the operator an answer");
    }

    /// <summary>
    /// A park that left no checkpoint at all is a known, reported class — there is nothing for
    /// an answer to be delivered to, so nothing ever enqueues a relaunch for it. The polarity
    /// is chosen so it keeps SAYING so rather than being quietly repainted as waiting for room.
    /// </summary>
    [Fact]
    public async Task RunSnapshot_AParkWithNoCheckpointAtAll_CarriesNoPosition()
    {
        await AddRunAsync("run-unanswerable", "waiting_for_input", ticketId: "43");
        await EnqueueResumeAsync("99", "run-other");

        var (active, _) = await BuildOverviewAsync();

        active.Single(s => s.RunId == "run-unanswerable").QueuePosition.Should().BeNull();
    }

    [Fact]
    public async Task RunSnapshot_AFinishedRun_CarriesNoPosition()
    {
        await AddRunAsync("run-done", "success", ticketId: "44", finished: true);

        var (_, recent) = await BuildOverviewAsync();

        recent.Single(s => s.RunId == "run-done").QueuePosition.Should().BeNull(
            "only a queued or a relaunching run waits for anything");
    }

    /// <summary>
    /// The detail endpoint composes its own snapshot rather than reading the overview builder,
    /// so the gate they SHARE is what keeps one page from disagreeing with the other.
    /// </summary>
    [Fact]
    public async Task RunDetail_AWaitingRunWithAQueueEntry_CarriesItsPosition()
    {
        await AddRunAsync("run-parked", "waiting_for_input", ticketId: "42");
        await EnqueueResumeAsync("42", "run-parked");
        var positions = await _queue.GetPositionsByRunIdAsync(CancellationToken.None);
        var run = await NewRepository().GetRunDetailAsync("run-parked", CancellationToken.None);

        var relaunching = await RunQueuePlace.RelaunchingAsync(
            Lease(), TimeProvider.System, positions, [run!], CancellationToken.None);

        RunQueuePlace.Of(run!, positions, relaunching).Should().Be(1);
    }

    /// <summary>
    /// The launcher claims the lease BEFORE it enqueues the job and BEFORE it removes the queue
    /// entry, so the two carriers overlap with no gap. Without this arm a free-capacity relaunch
    /// would show queued for one pump tick and then flip back to "needs you" for the whole
    /// launch-to-start span — which an operator reads as a SECOND question.
    /// </summary>
    [Fact]
    public async Task RunSnapshot_AWaitingRunWhoseRelaunchHoldsItsLease_ReportsItRelaunching()
    {
        await AddRunAsync("run-parked", "waiting_for_input", ticketId: "42");
        (await Lease().TryClaimAsync(Project, new TicketId("42"), CancellationToken.None))
            .Should().Be(LeaseClaimOutcome.Claimed);

        var (active, _) = await BuildOverviewAsync();

        active.Single().QueuePosition.Should().Be(
            RunQueuePlace.RelaunchUnderWay,
            "the entry is gone but the launcher holds the claim — under way, with no place in line");
    }

    [Fact]
    public async Task RunSnapshot_AWaitingRunWithNeitherPlaceNorLease_ReportsNeither()
    {
        await AddRunAsync("run-parked", "waiting_for_input", ticketId: "42");

        var (active, _) = await BuildOverviewAsync();

        active.Single().QueuePosition.Should().BeNull();
    }

    /// <summary>
    /// A lease that names ANOTHER run is a duplicate trigger that started its own run on the
    /// ticket, not this park's relaunch. The parked run keeps saying it needs an answer.
    /// </summary>
    [Fact]
    public async Task RunSnapshot_ALeaseHeldByAnotherRun_ReportsNothingForThePark()
    {
        await AddRunAsync("run-parked", "waiting_for_input", ticketId: "42");
        var lease = Lease();
        await lease.TryClaimAsync(Project, new TicketId("42"), CancellationToken.None);
        await lease.AttachRunAsync(Project, new TicketId("42"), "run-someone-else", null, CancellationToken.None);

        var (active, _) = await BuildOverviewAsync();

        active.Single(s => s.RunId == "run-parked").QueuePosition.Should().BeNull();
    }

    /// <summary>
    /// The status is NOT written on the relaunch path — it is the launcher's launch condition,
    /// and a run repainted as queued would be skipped in silence on every pump tick.
    /// </summary>
    [Fact]
    public async Task ResumeLauncher_ARunWhoseRelaunchIsQueued_StillLaunchesFromWaitingForInput()
    {
        await AddRunAsync("run-parked", "waiting_for_input", ticketId: "42");
        await EnqueueResumeAsync("42", "run-parked");
        (await BuildOverviewAsync()).Active.Single().QueuePosition.Should().Be(
            1, "the surface says queued while the row still says waiting_for_input");
        var jobQueue = new Mock<IRedisJobQueue>();
        var launcher = new ResumeRunLauncher(
            BuildProvider(), new NoOpActiveRunLease(), jobQueue.Object, _queue,
            NullLogger<ResumeRunLauncher>.Instance);

        await launcher.LaunchAsync(
            (await _queue.PeekHeadAsync(CancellationToken.None))!, CancellationToken.None);

        jobQueue.Verify(
            q => q.EnqueueAsync(It.Is<PipelineRequest>(r => r.RunId == "run-parked"), It.IsAny<CancellationToken>()),
            Times.Once,
            "the launch gate still reads waiting_for_input off the untouched run row");
        using var ctx = new AgentSmithDbContext(Options());
        ctx.QueuedTickets.Should().BeEmpty("the launched entry leaves the queue");
    }

    private Task<(RunSnapshot[] Active, RunSnapshot[] Recent)> BuildOverviewAsync() =>
        RunListComposer.BuildOverviewAsync(
            NewRepository(), _queue, CancellationToken.None, activeRunLease: Lease(),
            clock: TimeProvider.System);

    private Task<string> EnqueueResumeAsync(string ticketId, string runId) =>
        _queue.EnqueueAsync(new CapacityQueueCandidate(
            Project, ticketId, "code", "github", runId, "resuming after an answer",
            ["repo-a"], InitialContextJson: "{}", PlanAnswersJson: null, IsResume: true),
            CancellationToken.None);

    private async Task AddRunAsync(string runId, string status, string ticketId, bool finished = false)
    {
        using var ctx = new AgentSmithDbContext(Options());
        ctx.Runs.Add(new Run
        {
            Id = runId, Project = Project, Pipeline = "code", TicketId = ticketId,
            Status = status, StartedAt = DateTimeOffset.UtcNow,
            FinishedAt = finished ? DateTimeOffset.UtcNow : null,
        });
        await ctx.SaveChangesAsync();
    }

    private IActiveRunLease Lease() => new DbActiveRunLease(ScopeFactory());

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;

    private RunRepository NewRepository() => new(new AgentSmithDbContext(Options()));

    private IServiceScopeFactory ScopeFactory() =>
        BuildProvider().GetRequiredService<IServiceScopeFactory>();

    private IServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
        services.AddScoped<IUnitOfWork>(_ => new AgentSmithDbContext(Options()));
        services.AddSingleton<IUniqueViolationTranslator>(new SqliteUniqueViolationTranslator());
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<QueuedTicketRepository>();
        services.AddScoped<RunRepository>();
        services.AddScoped<ActiveRunRepository>();
        services.AddScoped<ActiveRunLivenessRepository>();
        return services.BuildServiceProvider();
    }
}
