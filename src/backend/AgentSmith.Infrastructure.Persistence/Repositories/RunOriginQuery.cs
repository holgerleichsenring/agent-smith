using AgentSmith.Contracts.Commands;
using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-11-24-6357: narrows a run query to the runs a person started (work) or the server started itself (system), by
/// the origin each preset declares, so the runs list can count the two apart.
/// </summary>
public static class RunOriginQuery
{
    public static IQueryable<Run> OfOrigin(this IQueryable<Run> runs, PipelineOrigin? origin)
    {
        if (origin is null) return runs;
        var system = PipelinePresets.SystemNames.ToList();
        return origin == PipelineOrigin.System
            ? runs.Where(r => system.Contains(r.Pipeline))
            : runs.Where(r => !system.Contains(r.Pipeline));
    }
}
