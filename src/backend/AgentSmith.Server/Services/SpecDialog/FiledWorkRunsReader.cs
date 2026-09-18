using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RunPullRequestView = AgentSmith.Contracts.Runs.RunPullRequestView;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-17-042ej: one work ticket's runs across the projects of one tracker, newest first
/// and capped, each with the phases it worked. Three set-based queries over the run ids —
/// never one per phase, because this read runs on every nudge window of a live run.
/// </summary>
public sealed class FiledWorkRunsReader(
    IServiceScopeFactory scopeFactory,
    FiledWorkPhaseReviews reviews,
    IRunCheckpointStore checkpoints,
    ILogger<FiledWorkRunsReader> logger)
{
    /// <summary>What an operator can still reason about; an older attempt is opened in the run view.</summary>
    private const int Cap = 10;

    public async Task<IReadOnlyList<FiledWorkRunView>> ForAsync(
        IReadOnlyList<FiledWorkProject> projects, string ticketId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(projects);
        if (projects.Count == 0) return [];
        var names = projects.Select(p => p.Name).ToList();
        using var scope = scopeFactory.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var runs = await uow.Set<Run>().AsNoTracking()
            .Where(r => names.Contains(r.Project) && r.TicketId == ticketId)
            // The run id is sortable (p0156), so newest-first needs no DateTimeOffset in the
            // ORDER BY — which SQLite cannot translate at all.
            .OrderByDescending(r => r.Id)
            .Take(Cap).ToListAsync(ct);
        if (runs.Count == 0) return [];

        var phases = await PhasesAsync(uow, runs.Select(r => r.Id).ToList(), ct);
        var composed = new List<FiledWorkRunView>(runs.Count);
        foreach (var run in runs) composed.Add(await ComposeAsync(run, phases, ct));
        return composed;
    }

    private async Task<FiledWorkRunView> ComposeAsync(
        Run run, ILookup<string, FiledWorkPhaseView> phases, CancellationToken ct) =>
        new(run.Id, run.Project, run.Pipeline, run.Status, run.CostTotalUsd,
            run.StartedAt, run.FinishedAt,
            RunStoryJson.TryDeserialize<List<RunPullRequestView>>(run.PullRequestsJson) ?? [],
            [.. phases[run.Id]],
            await QuestionAsync(run, ct));

    /// <summary>
    /// The question a parked run is waiting on, through the store that owns checkpoints — so
    /// the entity-to-record mapping lives in one place and cannot drift into a second copy
    /// whose four adjacent strings a re-ordering would silently transpose. Asked only of a
    /// waiting run, so a conversation's ten runs cost at most the parked ones.
    /// </summary>
    private async Task<PendingQuestionInfo?> QuestionAsync(Run run, CancellationToken ct) =>
        run.Status == RunStatuses.WaitingForInput
            ? PendingQuestionInfo.FromCheckpoint(
                await checkpoints.GetByRunIdAsync(run.Id, ct), logger)
            : null;

    private async Task<ILookup<string, FiledWorkPhaseView>> PhasesAsync(
        IUnitOfWork uow, List<string> ids, CancellationToken ct)
    {
        var rows = await uow.Set<RunPhase>().AsNoTracking()
            .Where(p => ids.Contains(p.RunId))
            .OrderBy(p => p.Ordinal).ThenBy(p => p.Id).ToListAsync(ct);
        var kinds = rows.Select(p => RunPhaseProjection.ReviewKindPrefix + p.PhaseId).Distinct().ToList();
        var stored = await uow.Set<RunArtifact>().AsNoTracking()
            .Where(a => ids.Contains(a.RunId) && kinds.Contains(a.Kind))
            .ToListAsync(ct);
        return rows.ToLookup(p => p.RunId, p => Phase(p, stored), StringComparer.Ordinal);
    }

    /// <summary>The two states RunPhaseProjection.StatusOf writes for a phase still going.</summary>
    private static readonly string[] Running = ["not_started", "in_progress"];

    private FiledWorkPhaseView Phase(RunPhase phase, List<RunArtifact> stored)
    {
        // The projection KEEPS the last verdict it saw and clears EndedAt when a phase runs
        // again, so a verdict on a running row is what stopped the attempt before. Terminal is
        // stated as "not running" rather than as a list of the three terminal words: 0e79c
        // appended "handed_back" to that list, and a fourth would otherwise silently hide the
        // verdict of a state this reader had not been taught.
        var terminal = !Running.Contains(phase.Status, StringComparer.Ordinal);
        var kind = RunPhaseProjection.ReviewKindPrefix + phase.PhaseId;
        var row = stored.FirstOrDefault(a => a.RunId == phase.RunId && a.Kind == kind);
        return new FiledWorkPhaseView(
            phase.PhaseId, phase.Ordinal, phase.Title, phase.Status,
            terminal ? phase.Verdict : null,
            reviews.Of(row?.Content, phase.PhaseId));
    }
}
