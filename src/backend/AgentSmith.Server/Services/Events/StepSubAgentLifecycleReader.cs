using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Services.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.Events;

/// <summary>
/// 2026-10-13-3890e: a step's sub-agent lifecycle — Spawned and Completed — older than the page
/// the step was opened on. The tree builds a sub-agent node only from its Spawned event, so a long
/// step opened on its newest page showed none of the sub-agents its LLM count already included.
/// These are a handful per step; the page's cursors stay the page's own.
/// </summary>
public sealed class StepSubAgentLifecycleReader(IServiceScopeFactory scopeFactory, EventEnvelopeSerializer envelopes)
{
    private static readonly string[] LifecycleTypes = [nameof(EventType.SubAgentSpawned), nameof(EventType.SubAgentCompleted)];

    /// <summary>The step's Spawned/Completed events with a sequence below <paramref name="belowSeq"/>, oldest first.</summary>
    public async Task<IReadOnlyList<object>> ReadOlderAsync(
        string runId, int stepIndex, long belowSeq, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var rows = await scope.ServiceProvider.GetRequiredService<IUnitOfWork>()
            .Set<Infrastructure.Persistence.Entities.RunEvent>()
            .AsNoTracking()
            .Where(e => e.RunId == runId && e.StepIndex == stepIndex && e.Seq < belowSeq && LifecycleTypes.Contains(e.Type))
            .OrderBy(e => e.Seq)
            .ToListAsync(cancellationToken);
        return [.. rows.Select(r => envelopes.DeserializeRaw(r.Type, r.PayloadJson)).OfType<object>()];
    }
}
