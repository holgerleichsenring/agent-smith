using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-11-10-fcd7b: what a design turn may do beyond reading, decided in one place — only for a poster who holds
/// runs.control (the seeded <see cref="ContextKeys.MayStartRuns"/>). A project memory reaches every later coding
/// prompt, so writing one is as consequential as starting a run.
/// <para>
/// 2026-11-10-4778: and run_command in a work clone, where three more things hold: the runtime spawns containers
/// (in-process, a shell would be the server's own user), the bus gives each sandbox its own credential (a
/// composition that cannot say — the CLI — counts as shared), and the turn carries its work clones.
/// </para>
/// </summary>
public sealed class DesignWorkToolGate(SandboxContainerRuntime? runtime = null, ISandboxBusIsolation? bus = null)
{
    public IEnumerable<AITool> Tools(PipelineContext pipeline, MasterRecordTools records)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(records);
        if (!MayStartRuns(pipeline)) return [];
        return [.. records.ProjectRemember?.GetTools(null, null) ?? [], .. Work(pipeline)];
    }

    public bool MayRun(PipelineContext pipeline) =>
        MayStartRuns(pipeline) && runtime?.SpawnsContainers == true && bus is { IsShared: false }
        && pipeline.TryGet<WorkCloneSet>(ContextKeys.WorkClones, out var clones) && clones is not null;

    public static bool MayStartRuns(PipelineContext pipeline) =>
        pipeline.TryGet<bool>(ContextKeys.MayStartRuns, out var may) && may;

    private IEnumerable<AITool> Work(PipelineContext pipeline)
    {
        if (!MayRun(pipeline)) return [];
        var held = pipeline.TryGet<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes, out var map) && map is not null
            ? map : new Dictionary<string, ISandbox>();
        var timeout = new RunCommandTimeout(
            pipeline.TryGet<int>(ContextKeys.RunCommandTimeoutSeconds, out var run) ? run : null,
            pipeline.TryGet<int>(ContextKeys.StepTimeoutSeconds, out var cap) ? cap : null);
        return new DesignWorkToolHost(pipeline.Get<WorkCloneSet>(ContextKeys.WorkClones), held, timeout).GetTools(null, null);
    }
}
