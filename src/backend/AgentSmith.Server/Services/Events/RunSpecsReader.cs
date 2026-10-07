using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.Events;

/// <summary>
/// p0466: serves a run's specs from the RunSpec projection, each with the decisions
/// and steps that name it (their PhaseId column keeps its stored name). Two set-based
/// queries plus the rail the step reader already composes — flat in the number of specs,
/// never one query per spec.
/// </summary>
public sealed class RunSpecsReader(IServiceScopeFactory scopeFactory, RunStepsReader steps)
{
    public async Task<IReadOnlyList<RunSpecView>> ReadAsync(string runId, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var specs = await uow.Set<RunSpec>().AsNoTracking()
            .Where(p => p.RunId == runId)
            .OrderBy(p => p.Ordinal).ThenBy(p => p.Id)
            .ToListAsync(ct);
        if (specs.Count == 0) return [];

        var decisions = await ReadDecisionsAsync(uow, runId, ct);
        var rail = await steps.ReadAsync(runId, ct);
        return [.. specs.Select(p => Compose(p, decisions, rail))];
    }

    /// <summary>
    /// p0466: one spec row with the body it executed. Null when the run holds no spec of
    /// that id — the endpoint answers 404 rather than an empty spec.
    /// </summary>
    public async Task<RunSpecDetailView?> ReadOneAsync(
        string runId, string specId, CancellationToken ct)
    {
        var spec = (await ReadAsync(runId, ct))
            .FirstOrDefault(p => string.Equals(p.SpecId, specId, StringComparison.Ordinal));
        if (spec is null) return null;

        using var scope = scopeFactory.CreateScope();
        var record = await scope.ServiceProvider.GetRequiredService<RunArtifactRepository>()
            .ReadAsync(runId, RunSpecProjection.RecordKindPrefix + specId, ct);
        return new RunSpecDetailView(spec, record);
    }

    private static RunSpecView Compose(
        RunSpec spec,
        ILookup<string, RunDecisionView> decisions,
        IReadOnlyList<RunStepView> rail) =>
        new(spec.SpecId, spec.Ordinal, spec.Title, spec.Status,
            spec.StartedAt, spec.EndedAt, spec.Verdict,
            [.. decisions[spec.SpecId]],
            [.. rail.Where(s => s.PhaseId == spec.SpecId)]);

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
