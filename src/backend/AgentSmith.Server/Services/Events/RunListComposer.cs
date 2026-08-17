using System.Globalization;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.Events;

/// <summary>
/// Composes the dashboard's run LIST out of the DB system-of-record and the live joins the
/// stored row cannot carry: the queue position of a queued run (p0320d — never persisted,
/// because the head moves), the pending question of a parked one (p0327) and the capacity
/// footprint (p0336).
/// <para>
/// Serving a list and composing its rows are two responsibilities; this is the second one,
/// so <see cref="Extensions.RunQueryEndpoints"/> only routes.
/// </para>
/// </summary>
internal static class RunListComposer
{
    // Matches the prior Redis-backed Recent window (JobsBroadcaster retained 50;
    // the dashboard caps the visible list client-side at 20/50 under ?debug=1).
    internal const int RecentLimit = 50;

    // p0355: bound for a "load more" page — clamp so a bad/huge limit can't scan away.
    internal const int MaxPageLimit = 200;

    internal static bool TryParseCursor(string before, out DateTimeOffset cursor) =>
        DateTimeOffset.TryParse(
            before, CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out cursor);

    // p0355: a cursor page of finished runs. No queue position or pending question (the
    // page is finished runs), but the capacity footprint is joined so the detail panel
    // stays complete when opened from a paged row.
    internal static async Task<RunSnapshot[]> BuildPageBeforeAsync(
        RunRepository runs, ICapacityBudget? capacityBudget, DateTimeOffset before, int limit,
        string? orchestratorMemoryRequest, CancellationToken ct)
    {
        var page = await runs.GetRunsBeforeAsync(before, limit, ct);
        var footprints = capacityBudget is null
            ? new Dictionary<string, RunCapacitySnapshot>()
            : await capacityBudget.GetManyAsync(page.Select(r => r.Id).ToList(), ct);
        return [.. page.Select(r => RunSnapshotMapper.ToSnapshot(
            r, null, orchestratorMemoryRequest, null, footprints.GetValueOrDefault(r.Id)))];
    }

    /// <summary>
    /// p0332: orchestratorMemoryRequest feeds the reserved resource-time — the same
    /// JobSpawner Resources value the spawner sizes the orchestrator pod with.
    /// </summary>
    internal static async Task<(RunSnapshot[] Active, RunSnapshot[] Recent)> BuildOverviewAsync(
        RunRepository runs, ICapacityQueue capacityQueue, CancellationToken cancellationToken,
        string? orchestratorMemoryRequest = null, IRunCheckpointStore? checkpoints = null,
        ICapacityBudget? capacityBudget = null)
    {
        var active = await runs.GetActiveRunsAsync(cancellationToken);
        var recent = await runs.GetRecentRunsAsync(RecentLimit, cancellationToken);
        var positions = await capacityQueue.GetPositionsByRunIdAsync(cancellationToken);
        // p0327: waiting_for_input runs carry their pending question so the list AND the
        // detail (both read this overview) render the answer affordance.
        var pending = await PendingQuestionsByRunIdAsync(checkpoints, cancellationToken);
        // p0336: the capacity footprint for every shown run — the detail panel reads it
        // off the overview, so it must be joined here, not only on detail.
        var footprints = await FootprintsByRunIdAsync(capacityBudget, active, recent, cancellationToken);
        return (
            [.. active.Select(r => RunSnapshotMapper.ToSnapshot(
                r, PositionOf(r, positions), orchestratorMemoryRequest,
                pending.GetValueOrDefault(r.Id), footprints.GetValueOrDefault(r.Id)))],
            [.. recent.Select(r => RunSnapshotMapper.ToSnapshot(
                r, PositionOf(r, positions), orchestratorMemoryRequest,
                null, footprints.GetValueOrDefault(r.Id)))]);
    }

    internal static int? PositionOf(Run run, IReadOnlyDictionary<string, int> positions) =>
        run.Status == "queued" && positions.TryGetValue(run.Id, out var position)
            ? position
            : null;

    private static async Task<IReadOnlyDictionary<string, RunCapacitySnapshot>> FootprintsByRunIdAsync(
        ICapacityBudget? capacityBudget, List<Run> active, List<Run> recent, CancellationToken ct)
    {
        if (capacityBudget is null) return new Dictionary<string, RunCapacitySnapshot>();
        var ids = active.Concat(recent).Select(r => r.Id).ToList();
        return await capacityBudget.GetManyAsync(ids, ct);
    }

    private static async Task<IReadOnlyDictionary<string, PendingQuestionInfo>> PendingQuestionsByRunIdAsync(
        IRunCheckpointStore? checkpoints, CancellationToken cancellationToken)
    {
        if (checkpoints is null) return new Dictionary<string, PendingQuestionInfo>();
        var pending = await checkpoints.ListPendingAsync(cancellationToken);
        return pending
            .Select(c => (c.RunId, Info: PendingQuestionInfo.FromCheckpoint(c)))
            .Where(x => x.Info is not null)
            .ToDictionary(x => x.RunId, x => x.Info!);
    }
}
