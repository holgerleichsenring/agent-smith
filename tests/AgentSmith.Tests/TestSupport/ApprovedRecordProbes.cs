using AgentSmith.Application.Services.Persistence;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Specs;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// 2026-09-25-3c7aa: the probe the routed paths ask whether an approved specification exists.
/// Most tests want one that answers NO for everything — the state every ticket was in before this
/// phase — so they say nothing and get exactly today's behaviour.
/// </summary>
internal static class ApprovedRecordProbes
{
    /// <summary>A probe over an empty store: no ticket has a record.</summary>
    internal static ApprovedRecordProbe None() => Over(new InMemorySpecApprovalStore());

    /// <summary>A probe over a store holding one record, keyed as the routed paths key it.</summary>
    internal static ApprovedRecordProbe Holding(
        string tracker, string platform, string ticketId)
    {
        var store = new InMemorySpecApprovalStore();
        var key = SpecSetKey.For(platform, ticketId);
        store.SaveAsync(
            ApprovedSets.Record(key.Value, ApprovedSets.Noon, tracker: tracker),
            CancellationToken.None).GetAwaiter().GetResult();
        return Over(store);
    }

    private static ApprovedRecordProbe Over(ISpecApprovalStore store) =>
        new(store, NullLogger<ApprovedRecordProbe>.Instance);
}
