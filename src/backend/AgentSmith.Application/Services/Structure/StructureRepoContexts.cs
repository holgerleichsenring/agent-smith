using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Structure;

/// <summary>
/// Every context of ONE repository, for the commit gate: the run's remote inventory of the
/// repository (all its contexts, also those scoping left without a sandbox) and the context
/// list of each sandbox the repository owns. A file is judged by the context whose workdir
/// holds it, so a context missing here would hand its files to the context around it.
/// </summary>
public static class StructureRepoContexts
{
    public static IReadOnlyList<RemoteContextDiscovery> Of(
        PipelineContext pipeline, string repoName, IEnumerable<string> sandboxKeys)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        pipeline.TryGet<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(
            ContextKeys.RemoteContextInventory, out var inventory);
        pipeline.TryGet<IReadOnlyDictionary<string, RemoteContextDiscovery>>(
            ContextKeys.SandboxDiscoveries, out var discoveries);
        var inSandboxes = sandboxKeys.SelectMany(key =>
            SandboxContextList.InOr(pipeline, key, discoveries?.GetValueOrDefault(key)));
        var inRepo = inventory?.GetValueOrDefault(repoName) ?? [];
        return [.. inSandboxes.Concat(inRepo).DistinctBy(c => c.ContextName, StringComparer.Ordinal)];
    }
}
