using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-10-02-075dc: the tools a design turn gets over its uploads, beyond reading and rendering
/// them — run_in_reference. Present only on a turn that has an upload, and only where sandboxes
/// are containers: the in-process backend would run the command inside the server.
/// </summary>
public sealed class ReferenceDesignTools(SandboxContainerRuntime runtime)
{
    public IReadOnlyList<AITool> For(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!runtime.SpawnsContainers) return [];
        var sandboxes = pipeline.TryGet<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes, out var map) && map is not null
            ? map : new Dictionary<string, ISandbox>();
        var references = sandboxes.Where(s => s.Key.StartsWith(ReferenceScopeName.Prefix, StringComparison.Ordinal))
            .ToDictionary(s => s.Key, s => s.Value, StringComparer.Ordinal);
        if (references.Count == 0) return [];
        var timeout = new RunCommandTimeout(
            pipeline.TryGet<int>(ContextKeys.RunCommandTimeoutSeconds, out var run) ? run : null,
            pipeline.TryGet<int>(ContextKeys.StepTimeoutSeconds, out var cap) ? cap : null);
        return [.. new ReferenceCommandToolHost(references, timeout).GetTools(null, null)];
    }
}
