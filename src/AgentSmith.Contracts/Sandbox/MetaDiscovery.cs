namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// One discovered context inside a cloned repo's sandbox (p0161). Produced by
/// IProjectMetaResolver.ResolveAllAsync via ISandboxFileReader after the
/// sandbox is up. One MetaDiscovery per `.agentsmith/contexts/&lt;name&gt;/`
/// sub-directory found under /work.
/// </summary>
/// <param name="MetaDir">Absolute path to the context's `.agentsmith/contexts/&lt;name&gt;/` dir inside the sandbox.</param>
/// <param name="ContextName">Sub-directory name (the context key).</param>
/// <param name="Workdir">`meta.workdir:` from that context's context.yaml — sub-tree the stack lives in, relative to repo root.</param>
public sealed record MetaDiscovery(string MetaDir, string ContextName, string Workdir);
