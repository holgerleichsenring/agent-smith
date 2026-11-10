using AgentSmith.Contracts.Commands;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-11-10-fcd7b: what a design turn may do beyond reading, decided in one place — only for a poster who holds
/// runs.control (the seeded <see cref="ContextKeys.MayStartRuns"/>). Today that is remember on the project: a
/// project memory reaches every later coding prompt, so writing one is as consequential as starting a run.
/// </summary>
public sealed class DesignWorkToolGate
{
    public IEnumerable<AITool> Tools(PipelineContext pipeline, MasterRecordTools records)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(records);
        if (!MayStartRuns(pipeline)) return [];
        return records.ProjectRemember?.GetTools(null, null) ?? [];
    }

    public static bool MayStartRuns(PipelineContext pipeline) =>
        pipeline.TryGet<bool>(ContextKeys.MayStartRuns, out var may) && may;
}
