using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Pocs.Rollout;

/// <summary>
/// 2026-11-23-c81ec: a follow's deadline — the smaller of pocs.rollout.deadline_seconds and the run's remaining wall
/// time less pocs.rollout.end_margin_seconds. The remaining wall time is pocs.run_wall_time_seconds less (now -
/// Run.StartedAt - the run's model waits): the clock CancelEnforcer holds the run to, so a resumed run is not given time
/// the enforcer has already spent. Without a run row the run's own start in the context stands in.
/// </summary>
public sealed class PocRolloutDeadline(IPocRolloutStore store, TimeProvider clock, IModelRunSlots? slots = null)
{
    public async Task<PocRolloutBudget> OfAsync(PipelineContext pipeline, PocsConfig pocs, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(pocs);
        var now = clock.GetUtcNow();
        var own = new PocRolloutBudget(now.AddSeconds(pocs.Rollout.DeadlineSeconds),
            $"{PocRolloutConfig.DeadlineSecondsKey} ({pocs.Rollout.DeadlineSeconds} s)");
        if (await SpentAsync(pipeline, now, ct) is not { } spent) return own;
        var left = pocs.RunWallTimeSeconds - spent.TotalSeconds - pocs.Rollout.EndMarginSeconds;
        var wall = new PocRolloutBudget(now.AddSeconds(Math.Max(0, left)),
            $"the run's remaining wall time: {PocsConfig.RunWallTimeSecondsKey} ({pocs.RunWallTimeSeconds} s) less the "
            + $"{(int)spent.TotalSeconds} s the run has spent less {PocRolloutConfig.EndMarginSecondsKey} ({pocs.Rollout.EndMarginSeconds} s)");
        return wall.Until < own.Until ? wall : own;
    }

    private async Task<TimeSpan?> SpentAsync(PipelineContext pipeline, DateTimeOffset now, CancellationToken ct)
    {
        var runId = pipeline.TryGet<string>(ContextKeys.RunId, out var id) && !string.IsNullOrEmpty(id) ? id : null;
        var started = runId is null ? null : await store.RunStartedAtAsync(runId, ct);
        if (started is null && pipeline.TryGet<DateTimeOffset>(ContextKeys.RunStartedAt, out var own)) started = own;
        if (started is not { } at) return null;
        var waited = runId is null || slots is null ? TimeSpan.Zero
            : (await slots.WaitedAsync([runId], ct)).GetValueOrDefault(runId);
        return now - at - waited;
    }
}
