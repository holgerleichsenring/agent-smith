using System.Text.RegularExpressions;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Runs;
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
public sealed partial class RunStepsReader(
    IServiceScopeFactory scopeFactory, RunStepAggregatesReader aggregates, RunRailComposer rail)
{
    // p0395: the shape PipelineStepRunner.PhaseQualified writes for spliced phase
    // steps (p0393a) — "p19106a: Generate plan". The projection keeps the composed
    // name (run records are not rewritten); the read path splits it back apart.
    // p0466: a step now STATES its phase in its own column, so this pattern is the
    // fallback for pre-p0466 rows ONLY. Those rows are deliberately not backfilled —
    // writing a parsed prefix into the new column would launder a guess into a fact.
    [GeneratedRegex(@"^(p\d+[a-z]?): (.+)$")]
    private static partial Regex PhaseQualifiedRegex();

    public async Task<IReadOnlyList<RunStepView>> ReadAsync(string runId, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var steps = await ReadStepRowsAsync(uow, runId, ct);
        var llm = await aggregates.ReadLlmAsync(uow, runId, ct);
        var counts = await aggregates.ReadEventCountsAsync(uow, runId, ct);
        var executed = steps.Select(s => Compose(s, llm, counts)).ToList();
        // p0405: the announced tail rides on the run row, so "what is still coming"
        // costs one more row read — never a second endpoint and never a client
        // rebuilding the sequence from a preset.
        var run = await uow.Set<Run>().AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == runId, ct);
        return rail.Compose(executed, run);
    }

    private static RunStepView Compose(
        RunStep step,
        IReadOnlyDictionary<int, (int Calls, decimal Cost)> llm,
        IReadOnlyDictionary<(int Step, string Type), int> counts)
    {
        var calls = llm.GetValueOrDefault(step.StepIndex);
        var (parsedPhaseId, stepName) = SplitPhase(step.StepName);
        var (displayPhaseId, displayName) = SplitPhase(step.DisplayName);
        var stepClass = CommandStepClasses.Get(step.CommandName);
        return new RunStepView(
            step.StepIndex, stepName ?? step.StepName, displayName, step.CommandName, step.Status,
            step.DurationSeconds, step.ResultMessage,
            calls.Calls, calls.Cost,
            counts.GetValueOrDefault((step.StepIndex, nameof(EventType.SandboxCommand))),
            counts.GetValueOrDefault((step.StepIndex, nameof(EventType.SubAgentSpawned))),
            step.PhaseId ?? parsedPhaseId ?? displayPhaseId,
            stepClass,
            stepClass == CommandStepClasses.Gate && GateHasFinding(step),
            // p0404: where this step's wall-clock went, from the time the applier
            // attributed to it as the calls and commands landed.
            RunTimeSplitView.From(step.LlmMs, step.ThrottleWaitMs, step.SandboxMs, step.DurationSeconds));
    }

    // p0398: a gate speaks when it failed or was cancelled mid-say, or when it
    // finished with a summary that is not one of its known no-op sentences. A
    // gate that has not finished yet has nothing to say — while it runs, the
    // drawer's live-status surfacing shows it, not this flag.
    private static bool GateHasFinding(RunStep step)
    {
        if (step.Status is "failed" or "cancelled") return true;
        return step.Status is "success"
            && !GateSilence.IsNoOpSummary(step.CommandName, step.ResultMessage);
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
}
