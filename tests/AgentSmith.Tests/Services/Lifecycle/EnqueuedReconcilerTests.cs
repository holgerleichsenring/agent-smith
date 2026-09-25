using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services.Lifecycle;

public sealed class EnqueuedReconcilerTests
{
    [Fact]
    public async Task EnqueuedWithoutFreshLease_ReEnqueues()
    {
        // p0252: no fresh lease (default Moq → GetByTicketAsync returns null) means
        // the run never started / died — re-enqueue. Liveness is the lease now.
        var harness = new Harness();
        harness.SetupEnqueuedTicket("42");

        await harness.BuildSut().RunAsync(OnePass());

        harness.JobQueue.Verify(q => q.EnqueueAsync(
            It.Is<PipelineRequest>(r => r.ProjectName == "proj"),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task EnqueuedWithFreshLease_NoReEnqueue()
    {
        // p0252: a fresh lease means a claim/run is already in flight — don't re-enqueue.
        var harness = new Harness();
        harness.SetupEnqueuedTicket("42");
        harness.Lease.Setup(l => l.GetByTicketAsync(
            "proj", It.IsAny<TicketId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StaleLease("proj", new TicketId("42"), "run-1", null, DateTimeOffset.UtcNow));

        await harness.BuildSut().RunAsync(OnePass());

        harness.JobQueue.Verify(q => q.EnqueueAsync(
            It.IsAny<PipelineRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnqueuedReconciler_TicketRoutesToOtherProject_NotReEnqueued()
    {
        // p0300c: on a shared tracker ListByLifecycleStatusAsync returns EVERY project's
        // Enqueued tickets. A ticket owned by another project must NOT be re-enqueued for
        // "proj" — the resolver returns no "proj" match, so the reconciler skips it.
        var harness = new Harness();
        harness.SetupEnqueuedTicket("42");
        harness.Resolver.Setup(r => r.Resolve(It.IsAny<AgentSmithConfig>(), It.IsAny<IncomingTicketEnvelope>()))
            .Returns([new ProjectMatch("other", "code", "github")]);

        await harness.BuildSut().RunAsync(OnePass());

        harness.JobQueue.Verify(q => q.EnqueueAsync(
            It.IsAny<PipelineRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// 2026-09-17-0e79a: the reconciler re-enqueues an orphan as a PipelineRequest with no context
    /// at all. It runs in the server, so it resolves the approved record and carries it — which is
    /// why the store fallback is not the only repair.
    /// </summary>
    [Fact]
    public async Task Reconciler_OrphanWithARecord_ReEnqueuesItWithTheRecord()
    {
        var harness = new Harness();
        harness.SetupEnqueuedTicket("42");
        var key = AgentSmith.Contracts.Specs.SpecSetKey.For("github", "42").Value;
        // The reconciler resolves by the project's tracker CONNECTION, which this bed leaves unnamed.
        await harness.Approvals.SaveAsync(
            TestSupport.ApprovedSets.Record(key, TestSupport.ApprovedSets.Noon, tracker: string.Empty),
            default);

        await harness.BuildSut().RunAsync(OnePass());

        harness.JobQueue.Verify(q => q.EnqueueAsync(
            It.Is<PipelineRequest>(r =>
                r.Context != null
                && r.Context.ContainsKey(AgentSmith.Contracts.Commands.ContextKeys.ApprovedSpecSet)),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    /// <summary>
    /// 2026-09-22-3f7c: the reconciler reconciles ONCE before it ever waits, and only then
    /// enters a ten-minute loop. A token that is already cancelled therefore buys exactly that
    /// pass and an immediate return — where a hundred-millisecond timer was a bet that the
    /// pass would finish first, and a bet the machine could lose.
    /// </summary>
    private static CancellationToken OnePass()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();
        return cts.Token;
    }

    private sealed class Harness
    {
        public Mock<IActiveRunLease> Lease { get; } = new();
        public Mock<IRedisJobQueue> JobQueue { get; } = new();
        public Mock<ITicketProviderFactory> TicketFactory { get; } = new();
        public Mock<ITicketProvider> Provider { get; } = new();
        public Mock<IConfigurationLoader> ConfigLoader { get; } = new();
        public Mock<IEnvelopeProjectResolver> Resolver { get; } = new();
        public AgentSmith.Contracts.Specs.ISpecApprovalStore Approvals { get; } =
            AgentSmith.Tests.TestSupport.ApprovedSetDoubles.Store();

        public Harness()
        {
            TicketFactory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(Provider.Object);
            ConfigLoader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(new AgentSmithConfig
            {
                Projects = new() { ["proj"] = new ResolvedProject { Pipeline = "code" } }
            });
            // Default: the ticket routes to THIS project (the lease tests exercise the
            // lease gate, not routing). Cross-project routing is overridden per-test.
            Resolver.Setup(r => r.Resolve(It.IsAny<AgentSmithConfig>(), It.IsAny<IncomingTicketEnvelope>()))
                .Returns([new ProjectMatch("proj", "code", "github")]);
        }

        public void SetupEnqueuedTicket(string id)
            => Provider.Setup(p => p.ListByLifecycleStatusAsync(
                TicketLifecycleStatus.Enqueued, It.IsAny<CancellationToken>()))
                .ReturnsAsync([new Ticket(new TicketId(id), "t", "d", null, "open", "GitHub")]);

        public EnqueuedReconciler BuildSut() => new(
            Lease.Object, JobQueue.Object, TicketFactory.Object,
            ConfigLoader.Object, Resolver.Object, TestSupport.ApprovedSetDoubles.Carrier(Approvals),
            TimeProvider.System, "config.yml",
            NullLogger<EnqueuedReconciler>.Instance);
    }
}
