using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Reads ContextKeys.Sandboxes + ContextKeys.SandboxDiscoveries from the
/// pipeline (both seeded by PipelineSandboxCoordinator after p0161a). Returns
/// false when either is missing — handlers turn that into a Skip/Fail per
/// their semantics.
/// </summary>
internal static class SandboxTargets
{
    public static bool TryResolve(
        PipelineContext pipeline,
        out IReadOnlyDictionary<string, ISandbox> sandboxes,
        out IReadOnlyDictionary<string, RemoteContextDiscovery> discoveries)
    {
        sandboxes = null!;
        discoveries = null!;
        if (!pipeline.TryGet<IReadOnlyDictionary<string, ISandbox>>(
                ContextKeys.Sandboxes, out var s) || s is null || s.Count == 0)
            return false;
        if (!pipeline.TryGet<IReadOnlyDictionary<string, RemoteContextDiscovery>>(
                ContextKeys.SandboxDiscoveries, out var d) || d is null)
            return false;
        sandboxes = s;
        discoveries = d;
        return true;
    }
}
