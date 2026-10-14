using AgentSmith.Contracts.Services;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Services.Rework;
using Moq;

namespace AgentSmith.Tests.TestSupport;

/// <summary>2026-10-13-8762v: a rework handler over its no-act tail; the series start is a no-op unless given.</summary>
internal static class ReworkHandlers
{
    public static ReworkNudgeHandler Of(
        IActiveRunLease leases, IPreviousAttemptReader attempts, IReworkPendingActs pending, IReworkEntry rework,
        ReworkSpeech speech, ReworkFallbackSpawn fallback, ReworkWithheld withheld, IRunCheckpointStore checkpoints,
        ClosedTicketActFollowUp followUps, ISeriesAutoStart? series = null) =>
        new(leases, attempts, pending, rework, speech, fallback, withheld, checkpoints, followUps,
            new ReworkNoActTail(pending, followUps, withheld, series ?? Mock.Of<ISeriesAutoStart>()));
}
