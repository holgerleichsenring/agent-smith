using AgentSmith.Application.Services.Claim;
using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Application.Services.Persistence;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services.Lifecycle;

/// <summary>
/// 2026-09-25-b4d9: a claim INSERTs the lease and then enqueues. A crash between the two used
/// to leave an unattached lease and no run row at all; the reaper deleted that lease three
/// minutes later, and the only surviving evidence of the orphan was a label on somebody else's
/// board. These tests pin the record that now outlives the reap.
/// </summary>
public sealed class OrphanedTicketRecordTests
{
    private static readonly ClaimRequest Claim =
        new("github", "proj", new TicketId("42"), "migrate-repo");

    [Fact]
    public async Task Orphan_ALeaseReapedAndTheLabelDeleted_IsStillReconciled()
    {
        var bed = new Bed();
        await bed.ClaimThenCrashAsync();
        await bed.ReapAsync();

        await bed.Reconciler().RunAsync(EnqueuedReconcilerTests.OnePass());

        bed.Queue.Verify(q => q.EnqueueAsync(
            It.Is<PipelineRequest>(r => r.TicketId!.Value == "42"),
            It.IsAny<CancellationToken>()), Times.Once,
            "the board is no longer the record — nothing was asked of it");
    }

    [Fact]
    public async Task Orphan_TheRebuiltRequest_CarriesThePipelineTheClaimKnew()
    {
        // The project's configured default is "fix-bug"; the claim was granted for
        // "migrate-repo". The lease has no pipeline column, so this used to come back from
        // the ticket's labels — and a label an operator edits would rebuild the wrong run.
        var bed = new Bed();
        await bed.ClaimThenCrashAsync();
        await bed.ReapAsync();

        await bed.Reconciler().RunAsync(EnqueuedReconcilerTests.OnePass());

        bed.Queue.Verify(q => q.EnqueueAsync(
            It.Is<PipelineRequest>(r => r.PipelineName == "migrate-repo"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Orphan_ATicketThatFinished_LeavesNoRecordBehind()
    {
        // The record is cleared by COMPLETION, not by a timer: a record deleted on a timer
        // is the same disappearing act one layer down.
        using var bed = new FinishedRunBed();
        await bed.Taken.TakeAsync(new TakenTicketFact("proj", "42", "github", "migrate-repo"), default);

        await bed.FinishAsync("completed");

        (await bed.Taken.ListReconcilableAsync(default)).Should().BeEmpty();
    }

    [Fact]
    public async Task Orphan_ATicketTheReaperIsCancelling_IsNotReEnqueued()
    {
        // The reaper scans every sixty seconds and the reconciler every ten minutes, and both
        // act on a ticket whose lease went stale. The record's state is what separates them.
        var bed = new Bed();
        await bed.ClaimThenCrashAsync();
        var reaping = bed.StartReapAsync();

        await bed.Reconciler().RunAsync(EnqueuedReconcilerTests.OnePass());

        bed.Queue.Verify(q => q.EnqueueAsync(
            It.IsAny<PipelineRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        await bed.FinishReapAsync(reaping);
        await bed.Reconciler().RunAsync(EnqueuedReconcilerTests.OnePass());
        bed.Queue.Verify(q => q.EnqueueAsync(
            It.IsAny<PipelineRequest>(), It.IsAny<CancellationToken>()), Times.Once,
            "the reap is over, so the ticket is the reconciler's again");
    }

    [Fact]
    public async Task Orphan_AClaimThatRolledItselfBack_LeavesNoRecordBehind()
    {
        // The claim region rolls both writes back when it fails after taking them, so a failed
        // claim leaves neither an orphan lease nor a record of work nobody is doing.
        var bed = new Bed();
        await bed.ClaimThenCrashAsync();

        await new ClaimedTicketRegistrar(bed.Lease.Object, bed.Taken).RollBackAsync(Claim, default);

        (await bed.Taken.ListReconcilableAsync(default)).Should().BeEmpty();
    }

    [Fact]
    public void Orphan_NoTrackerCall_IsMadeToFindCandidates()
    {
        // Not "does not call it" — CANNOT call it. The reconciler holds no ticket provider
        // and no envelope resolver, so its candidates cannot come from a board.
        var dependencies = typeof(EnqueuedReconciler).GetConstructors().Single()
            .GetParameters().Select(p => p.ParameterType).ToList();

        dependencies.Should().NotContain(typeof(ITicketProviderFactory))
            .And.NotContain(typeof(ITicketProvider))
            .And.NotContain(typeof(IEnvelopeProjectResolver));
    }

    private sealed class Bed
    {
        public Mock<IRedisJobQueue> Queue { get; } = new();
        public Mock<IActiveRunLease> Lease { get; } = new();
        public InMemoryTakenTicketStore Taken { get; } = new();
        private readonly TaskCompletionSource _releaseGate = new();

        public Bed() =>
            Lease.Setup(l => l.TryClaimAsync("proj", It.IsAny<TicketId>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(LeaseClaimOutcome.Claimed);

        /// <summary>The lease is taken and the record written; then the replica dies.</summary>
        public Task ClaimThenCrashAsync() =>
            new ClaimedTicketRegistrar(Lease.Object, Taken).TakeAsync(Claim, default);

        public Task ReapAsync() => Release().ReapAsync(Candidate(), default);

        public Task<bool> StartReapAsync()
        {
            // The release blocks, so the reap is still in flight while the reconciler runs.
            Lease.Setup(l => l.ReleaseAsync(
                    "proj", It.IsAny<TicketId>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .Returns(async () => { await _releaseGate.Task; return LeaseReleaseOutcome.Released; });
            return Release().ReapAsync(Candidate(), default);
        }

        public async Task FinishReapAsync(Task<bool> reaping)
        {
            _releaseGate.SetResult();
            (await reaping).Should().BeTrue();
        }

        public EnqueuedReconciler Reconciler() => new(
            Lease.Object, Queue.Object, Taken, ConfigLoader(),
            TestSupport.ApprovedSetDoubles.Carrier(TestSupport.ApprovedSetDoubles.Store()),
            TimeProvider.System, "config.yml", NullLogger<EnqueuedReconciler>.Instance);

        private StaleLeaseRelease Release() => new(
            Lease.Object, new Mock<IRunCancellationRegistry>().Object,
            new Mock<IEventPublisher>().Object, Taken, TimeProvider.System,
            NullLogger<StaleLeaseRelease>.Instance);

        private static StaleLease Candidate() => new("proj", new TicketId("42"), "run-1", null);

        private static IConfigurationLoader ConfigLoader()
        {
            var loader = new Mock<IConfigurationLoader>();
            loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(new AgentSmithConfig
            {
                Projects = new() { ["proj"] = new ResolvedProject { Pipeline = "fix-bug" } }
            });
            return loader.Object;
        }
    }

    private sealed class FinishedRunBed : IDisposable
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");

        public InMemoryTakenTicketStore Taken { get; } = new();

        public FinishedRunBed()
        {
            _connection.Open();
            using var ctx = Context();
            ctx.Database.Migrate();
            ctx.Runs.Add(new Run
            {
                Id = "run-1", Project = "proj", TicketId = "42", Status = "running",
                Pipeline = "migrate-repo", StartedAt = DateTimeOffset.UnixEpoch,
            });
            ctx.SaveChanges();
        }

        public async Task FinishAsync(string status)
        {
            using var ctx = Context();
            await new RunFinalizationProjection(new QueuedRunProjection(), takenTickets: Taken)
                .ApplyAsync(ctx, new RunFinishedEvent("run-1", status, null, "done", DateTimeOffset.UnixEpoch),
                    default);
        }

        public void Dispose() => _connection.Dispose();

        private AgentSmithDbContext Context() => new(
            new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
    }
}
