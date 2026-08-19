using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.Events;

/// <summary>
/// p0466: serves a run's phases from the RunPhase projection, each with the decisions
/// and steps that name it. Two set-based queries plus the rail the step reader already
/// composes — flat in the number of phases, never one query per phase.
/// </summary>
public sealed class RunPhasesReader(IServiceScopeFactory scopeFactory, RunStepsReader steps)
{
    public async Task<IReadOnlyList<RunPhaseView>> ReadAsync(string runId, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var phases = await uow.Set<RunPhase>().AsNoTracking()
            .Where(p => p.RunId == runId)
            .OrderBy(p => p.Ordinal).ThenBy(p => p.Id)
            .ToListAsync(ct);
        if (phases.Count == 0) return [];

        var decisions = await ReadDecisionsAsync(uow, runId, ct);
        var rail = await steps.ReadAsync(runId, ct);
        return [.. phases.Select(p => Compose(p, decisions, rail))];
    }

    /// <summary>
    /// p0466: one phase with the spec it executed. Null when the run holds no phase of
    /// that id — the endpoint answers 404 rather than an empty phase.
    /// </summary>
    public async Task<RunPhaseDetailView?> ReadOneAsync(
        string runId, string phaseId, CancellationToken ct)
    {
        var phase = (await ReadAsync(runId, ct))
            .FirstOrDefault(p => string.Equals(p.PhaseId, phaseId, StringComparison.Ordinal));
        if (phase is null) return null;

        using var scope = scopeFactory.CreateScope();
        var record = await scope.ServiceProvider.GetRequiredService<RunArtifactRepository>()
            .ReadAsync(runId, RunPhaseProjection.RecordKindPrefix + phaseId, ct);
        return new RunPhaseDetailView(phase, record);
    }

    private static RunPhaseView Compose(
        RunPhase phase,
        ILookup<string, RunDecisionView> decisions,
        IReadOnlyList<RunStepView> rail) =>
        new(phase.PhaseId, phase.Ordinal, phase.Title, phase.Status,
            phase.StartedAt, phase.EndedAt, phase.Verdict,
            [.. decisions[phase.PhaseId]],
            [.. rail.Where(s => s.PhaseId == phase.PhaseId)]);

    // A decision that names no phase belongs to no phase — it stays on the run's own
    // decision list rather than being attached to whichever phase shares its step.
    private static async Task<ILookup<string, RunDecisionView>> ReadDecisionsAsync(
        IUnitOfWork uow, string runId, CancellationToken ct)
    {
        var rows = await uow.Set<RunDecision>().AsNoTracking()
            .Where(d => d.RunId == runId && d.PhaseId != null)
            .OrderBy(d => d.Id)
            .ToListAsync(ct);
        return rows.ToLookup(
            d => d.PhaseId!,
            d => new RunDecisionView(d.StepIndex, d.Name, d.Reason, d.Category, d.CreatedAt),
            StringComparer.Ordinal);
    }
}
