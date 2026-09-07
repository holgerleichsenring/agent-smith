using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-09-04-0721: which contexts a sandbox holds. One sandbox is one toolchain image, so
/// several contexts of a repository share it, and <see cref="ContextKeys.SandboxDiscoveries"/>
/// carries only the FIRST of each group as its representative — the full list is
/// <see cref="ContextKeys.SandboxContexts"/>.
/// <para>
/// Every consumer that walks the contexts of a sandbox had written this lookup out again, and
/// the one that did not — the re-init short-circuit — bootstrapped a two-context repository's
/// first context while the gate went on probing both. One reading, so a new consumer inherits
/// the answer instead of choosing between two maps.
/// </para>
/// </summary>
public static class SandboxContextList
{
    /// <summary>Every context in the sandbox; empty when no list was published.</summary>
    public static IReadOnlyList<RemoteContextDiscovery> In(PipelineContext pipeline, string sandboxKey)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return pipeline.TryGet<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(
                   ContextKeys.SandboxContexts, out var bySandbox)
               && bySandbox is not null
               && bySandbox.TryGetValue(sandboxKey, out var contexts)
            ? contexts
            : [];
    }

    /// <summary>
    /// Every context in the sandbox, or the group's representative where no list exists — a
    /// run whose checkpoint predates the list, or a caller holding a discovery the coordinator
    /// never grouped.
    /// </summary>
    public static IReadOnlyList<RemoteContextDiscovery> InOr(
        PipelineContext pipeline, string sandboxKey, RemoteContextDiscovery? representative)
    {
        var contexts = In(pipeline, sandboxKey);
        if (contexts.Count > 0) return contexts;
        return representative is null ? [] : [representative];
    }
}
