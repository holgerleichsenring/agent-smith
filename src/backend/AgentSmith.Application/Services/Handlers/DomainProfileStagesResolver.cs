using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0504: the domain profiles that apply to one sandbox, in context order.
/// <para>
/// Reads the per-sandbox CONTEXT LIST, not the grouped representative: two contexts
/// sharing an image collapse into one sandbox, and reading the representative would
/// make whether a domain is honoured depend on discovery order.
/// </para>
/// </summary>
public sealed class DomainProfileStagesResolver(IDomainProfileCatalog catalog)
{
    public IReadOnlyList<DomainProfileStages> For(PipelineContext pipeline, string sandboxKey)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!pipeline.TryGet<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(
                ContextKeys.SandboxContexts, out var bySandbox)
            || bySandbox is null
            || !bySandbox.TryGetValue(sandboxKey, out var contexts))
            return [];

        var stages = new List<DomainProfileStages>();
        foreach (var context in contexts)
        {
            if (string.IsNullOrWhiteSpace(context.Domain)) continue;
            if (catalog.Find(context.Domain) is not { } profile) continue;
            stages.Add(new DomainProfileStages(profile, SandboxWorkdir.Resolve(context.Workdir)));
        }
        return stages;
    }
}
