using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-09-01-379a: the declared target probes that apply to one sandbox, in context order.
/// <para>
/// Reads the per-sandbox CONTEXT LIST (<see cref="ContextKeys.SandboxContexts"/>), not the
/// grouped representative at <see cref="ContextKeys.SandboxDiscoveries"/> — the distinction
/// <see cref="ContextVerifyStagesResolver"/> exists for: two contexts sharing an image
/// collapse into one sandbox, and reading the representative would make whose declaration
/// is honoured depend on discovery order.
/// </para>
/// </summary>
public sealed class ContextTargetProbeResolver
{
    public IReadOnlyList<ContextTargetProbe> For(PipelineContext pipeline, string sandboxKey)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!pipeline.TryGet<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(
                ContextKeys.SandboxContexts, out var bySandbox)
            || bySandbox is null
            || !bySandbox.TryGetValue(sandboxKey, out var contexts))
            return [];

        return [.. contexts
            .Where(context => context.Probe is not null)
            .Select(context => new ContextTargetProbe(
                context.ContextName, context.Probe!.Target, context.Probe.Command,
                SandboxWorkdir.Resolve(context.Workdir)))];
    }
}
