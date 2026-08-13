using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Server.Services.Events;

/// <summary>
/// p0404: the per-step aggregates over a run's p0388a-attributed child rows,
/// split out of <see cref="RunStepsReader"/> — the reader composes the rail, this
/// answers what the child rows add up to. Set-based: one query per aggregate for
/// the whole run, never one per step.
/// </summary>
public sealed class RunStepAggregatesReader
{
    /// <summary>
    /// Cost is summed in memory: SQLite has no native decimal, so a server-side
    /// Sum over the decimal column is not translatable (the same reason
    /// RunEventApplier materialises before summing). The projection is one row per
    /// LLM call, which is bounded by the run's call count, not its runtime.
    /// </summary>
    public async Task<IReadOnlyDictionary<int, (int Calls, decimal Cost)>> ReadLlmAsync(
        IUnitOfWork uow, string runId, CancellationToken ct)
    {
        var calls = await uow.Set<RunLlmCall>().AsNoTracking()
            .Where(c => c.RunId == runId && c.StepIndex != null)
            .Select(c => new { Step = c.StepIndex!.Value, c.CostUsd })
            .ToListAsync(ct);
        return calls
            .GroupBy(c => c.Step)
            .ToDictionary(g => g.Key, g => (g.Count(), g.Sum(x => x.CostUsd)));
    }

    public async Task<IReadOnlyDictionary<(int, string), int>> ReadEventCountsAsync(
        IUnitOfWork uow, string runId, CancellationToken ct)
    {
        var sandboxCommand = nameof(EventType.SandboxCommand);
        var subAgentSpawned = nameof(EventType.SubAgentSpawned);
        var grouped = await uow.Set<Infrastructure.Persistence.Entities.RunEvent>().AsNoTracking()
            .Where(e => e.RunId == runId && e.StepIndex != null
                        && (e.Type == sandboxCommand || e.Type == subAgentSpawned))
            .GroupBy(e => new { Step = e.StepIndex!.Value, e.Type })
            .Select(g => new { g.Key.Step, g.Key.Type, Count = g.Count() })
            .ToListAsync(ct);
        return grouped.ToDictionary(g => (g.Step, g.Type), g => g.Count);
    }
}
