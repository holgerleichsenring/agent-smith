using System.Text.RegularExpressions;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.Events;

/// <summary>
/// p0388b: reads the run's execution rail from the DB projections — the RunStep
/// rows plus the per-step aggregates over the p0388a-attributed child rows. Three
/// set-based queries per run, none per step, so the cost is flat in the number of
/// steps no matter how long the run ran.
/// </summary>
public sealed partial class RunStepsReader(IServiceScopeFactory scopeFactory)
{
    // p0395: the shape PipelineStepRunner.PhaseQualified writes for spliced phase
    // steps (p0393a) — "p19106a: Generate plan". The projection keeps the composed
    // name (run records are not rewritten); the read path splits it back apart.
    [GeneratedRegex(@"^(p\d+[a-z]?): (.+)$")]
    private static partial Regex PhaseQualifiedRegex();

    public async Task<IReadOnlyList<RunStepView>> ReadAsync(string runId, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var steps = await ReadStepRowsAsync(uow, runId, ct);
        var llm = await ReadLlmAggregatesAsync(uow, runId, ct);
        var counts = await ReadEventCountsAsync(uow, runId, ct);
        return steps.Select(s => Compose(s, llm, counts)).ToList();
    }

    private static RunStepView Compose(
        RunStep step,
        IReadOnlyDictionary<int, (int Calls, decimal Cost)> llm,
        IReadOnlyDictionary<(int Step, string Type), int> counts)
    {
        var calls = llm.GetValueOrDefault(step.StepIndex);
        var (phaseId, stepName) = SplitPhase(step.StepName);
        var (displayPhaseId, displayName) = SplitPhase(step.DisplayName);
        var stepClass = CommandStepClasses.Get(step.CommandName);
        return new RunStepView(
            step.StepIndex, stepName ?? step.StepName, displayName, step.CommandName, step.Status,
            step.DurationSeconds, step.ResultMessage,
            calls.Calls, calls.Cost,
            counts.GetValueOrDefault((step.StepIndex, nameof(EventType.SandboxCommand))),
            counts.GetValueOrDefault((step.StepIndex, nameof(EventType.SubAgentSpawned))),
            phaseId ?? displayPhaseId,
            stepClass,
            stepClass == CommandStepClasses.Gate && GateHasFinding(step));
    }

    // p0398: a gate speaks when it failed or was cancelled mid-say, or when it
    // finished with a summary that is not one of its known no-op sentences. A
    // gate that has not finished yet has nothing to say — while it runs, the
    // drawer's live-status surfacing shows it, not this flag.
    private static bool GateHasFinding(RunStep step)
    {
        if (step.Status is "failed" or "cancelled") return true;
        return step.Status is "success"
            && !CommandStepClasses.IsNoOpSummary(step.CommandName, step.ResultMessage);
    }

    private static (string? PhaseId, string? Name) SplitPhase(string? composed)
    {
        if (string.IsNullOrEmpty(composed)) return (null, composed);
        var match = PhaseQualifiedRegex().Match(composed);
        return match.Success ? (match.Groups[1].Value, match.Groups[2].Value) : (null, composed);
    }

    // One row per step index: the applier can leave a second row behind when a
    // StepFinished lands without its StepStarted, and the LATEST row (highest Id)
    // is the one carrying the finished status.
    private static async Task<List<RunStep>> ReadStepRowsAsync(
        IUnitOfWork uow, string runId, CancellationToken ct)
    {
        var rows = await uow.Set<RunStep>().AsNoTracking()
            .Where(s => s.RunId == runId)
            .OrderBy(s => s.StepIndex).ThenBy(s => s.Id)
            .ToListAsync(ct);
        return rows
            .GroupBy(s => s.StepIndex)
            .Select(g => g.Last())
            .OrderBy(s => s.StepIndex)
            .ToList();
    }

    // Cost is summed in memory: SQLite has no native decimal, so a server-side
    // Sum over the decimal column is not translatable (the same reason
    // RunEventApplier materialises before summing). The projection is one row per
    // LLM call, which is bounded by the run's call count, not its runtime.
    private static async Task<IReadOnlyDictionary<int, (int Calls, decimal Cost)>> ReadLlmAggregatesAsync(
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

    private static async Task<IReadOnlyDictionary<(int, string), int>> ReadEventCountsAsync(
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
