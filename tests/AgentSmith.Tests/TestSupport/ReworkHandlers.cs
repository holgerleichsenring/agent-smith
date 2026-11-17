using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Services.Rework;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.TestSupport;

/// <summary>2026-10-13-8762v: a rework handler over its no-act tail; the series start is a no-op unless given.
/// 2026-11-16-f3fbc: the precondition release launches nothing unless given.</summary>
internal static class ReworkHandlers
{
    public static ReworkNudgeHandler Of(
        IActiveRunLease leases, IPreviousAttemptReader attempts, IReworkPendingActs pending, IReworkEntry rework,
        ReworkSpeech speech, ReworkFallbackSpawn fallback, ReworkWithheld withheld, IRunCheckpointStore checkpoints,
        ClosedTicketActFollowUp followUps, ISeriesAutoStart? series = null, RequirementsMetStart? start = null,
        PreconditionRelease? release = null, StackedDependentsWake? stacked = null) =>
        Of(leases, attempts, pending, rework, speech, fallback, withheld, checkpoints, followUps,
            new ReworkNoActTail(pending, followUps, withheld, series ?? Mock.Of<ISeriesAutoStart>()), start, release,
            stacked ?? NoStackedWake(attempts, leases));

    private static ReworkNudgeHandler Of(
        IActiveRunLease leases, IPreviousAttemptReader attempts, IReworkPendingActs pending, IReworkEntry rework,
        ReworkSpeech speech, ReworkFallbackSpawn fallback, ReworkWithheld withheld, IRunCheckpointStore checkpoints,
        ClosedTicketActFollowUp followUps, ReworkNoActTail tail, RequirementsMetStart? start, PreconditionRelease? release,
        StackedDependentsWake stacked) =>
        new(leases, attempts, pending, rework, speech, fallback, withheld, checkpoints, followUps, tail, start ?? NoStart(fallback),
            release ?? new PreconditionRelease(attempts, pending, tail, Hold(new InMemoryTicketFacts()), Mock.Of<IReworkLaunch>(),
                NullLogger<PreconditionRelease>.Instance), stacked);

    // 2026-11-17-9a39a: a green wake over empty requirement stores — it never nudges anything.
    public static StackedDependentsWake NoStackedWake(IPreviousAttemptReader attempts, IActiveRunLease leases)
    {
        var facts = new InMemoryTicketFacts();
        return new(facts, facts, TicketRequirementDoubles.Of(facts), attempts, leases, Mock.Of<IReworkNudges>(),
            NullLogger<StackedDependentsWake>.Instance);
    }

    // 2026-10-14-dbceh: a start over empty requirement stores — it never starts anything.
    private static RequirementsMetStart NoStart(ReworkFallbackSpawn fallback)
    {
        var facts = new InMemoryTicketFacts();
        return new(facts, Hold(facts), TicketRequirementDoubles.Of(facts), Mock.Of<IServiceScopeFactory>(),
            fallback, NullLogger<RequirementsMetStart>.Instance);
    }

    public static TicketStartHold Hold(InMemoryTicketFacts facts, ITicketProviderFactory? trackers = null) =>
        new(facts, facts, trackers ?? Mock.Of<ITicketProviderFactory>(), NullLogger<TicketStartHold>.Instance);
}
