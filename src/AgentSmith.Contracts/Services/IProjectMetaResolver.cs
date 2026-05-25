using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Enumerates all .agentsmith/contexts/&lt;name&gt; sub-directories in the cloned
/// repo (p0161). Routes directory listings through ISandboxFileReader so the
/// lookup happens against the sandbox filesystem (/work). Returns one
/// MetaDiscovery per context.yaml found; empty list for un-init / pre-v2
/// repos.
/// </summary>
public interface IProjectMetaResolver
{
    Task<IReadOnlyList<MetaDiscovery>> ResolveAllAsync(
        ISandboxFileReader reader, CancellationToken cancellationToken);
}
