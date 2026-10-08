using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-10-02-075dc: the tools a design turn gets over its uploads, beyond reading and rendering
/// them — run_in_reference, only where sandboxes are containers: the in-process backend would run
/// the command inside the server. 2026-10-02-075dd: and note_reference, where a store keeps notes.
/// Nothing on a turn without an upload.
/// </summary>
public sealed class ReferenceDesignTools(
    SandboxContainerRuntime runtime, IReferenceNotes? notes = null,
    IReferenceSetReader? sets = null, IToolImageDeposit? images = null)
{
    public IReadOnlyList<AITool> For(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var sandboxes = pipeline.TryGet<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes, out var map) && map is not null
            ? map : new Dictionary<string, ISandbox>();
        var references = sandboxes.Where(s => s.Key.StartsWith(ReferenceScopeName.Prefix, StringComparison.Ordinal))
            .ToDictionary(s => s.Key, s => s.Value, StringComparer.Ordinal);
        if (references.Count == 0) return [];
        var tools = new List<AITool>();
        if (runtime.SpawnsContainers)
            tools.AddRange(new ReferenceCommandToolHost(references, new RunCommandTimeout(
                pipeline.TryGet<int>(ContextKeys.RunCommandTimeoutSeconds, out var run) ? run : null,
                pipeline.TryGet<int>(ContextKeys.StepTimeoutSeconds, out var cap) ? cap : null)).GetTools(null, null));
        var held = references.Where(r => r.Value is ReferenceSetSandbox)
            .ToDictionary(r => r.Key, r => (ReferenceSetSandbox)r.Value, StringComparer.Ordinal);
        if (notes is not null && held.Count > 0)
            tools.AddRange(new ReferenceNoteToolHost(held, notes).GetTools(null, null));
        // 2026-10-08-e8b9j: an image inside an upload, shown as a picture — on any backend.
        if (sets is not null && images is not null && held.Count > 0)
            tools.AddRange(new ReferenceImageToolHost(held.ToDictionary(r => r.Key,
                r => new ReferenceUploadAddress(r.Value.ConversationId, r.Value.SetId), StringComparer.Ordinal),
                sets, images).GetTools(null, null));
        return tools;
    }
}
