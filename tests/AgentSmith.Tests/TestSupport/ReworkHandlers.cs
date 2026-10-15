using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Services.Rework;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.TestSupport;

/// <summary>2026-10-13-8762v: a rework handler over its no-act tail; the series start is a no-op unless given.</summary>
internal static class ReworkHandlers
{
    public static ReworkNudgeHandler Of(
        IActiveRunLease leases, IPreviousAttemptReader attempts, IReworkPendingActs pending, IReworkEntry rework,
        ReworkSpeech speech, ReworkFallbackSpawn fallback, ReworkWithheld withheld, IRunCheckpointStore checkpoints,
        ClosedTicketActFollowUp followUps, ISeriesAutoStart? series = null, RequirementsMetStart? start = null) =>
        new(leases, attempts, pending, rework, speech, fallback, withheld, checkpoints, followUps,
            new ReworkNoActTail(pending, followUps, withheld, series ?? Mock.Of<ISeriesAutoStart>()), start ?? NoStart(fallback));

    // 2026-10-14-dbceh: a start over empty requirement stores — it never starts anything.
    private static RequirementsMetStart NoStart(ReworkFallbackSpawn fallback)
    {
        var facts = new InMemoryTicketFacts();
        return new(facts, facts, facts, TicketRequirementDoubles.Of(facts), Mock.Of<ITicketProviderFactory>(), Mock.Of<IServiceScopeFactory>(),
            fallback, NullLogger<RequirementsMetStart>.Instance);
    }
}
