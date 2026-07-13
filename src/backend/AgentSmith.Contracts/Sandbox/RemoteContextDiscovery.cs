using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// One discovered context fetched REMOTELY from a source provider (p0161),
/// before any sandbox exists. Produced by SandboxLanguageResolver.ResolveAllAsync
/// via ISourceProvider.ListDirectoryAsync + TryReadFileAsync. Drives
/// PipelineSandboxCoordinator's fan-out — one sandbox per discovery, each
/// with its own toolchain image.
/// </summary>
/// <param name="ContextName">Sub-directory name under `.agentsmith/contexts/` (the context key).</param>
/// <param name="Workdir">`meta.workdir:` from that context's context.yaml.</param>
/// <param name="Language">`stack.lang:` from that context's context.yaml; null = generic-image fallback.</param>
/// <param name="Prerequisites">`prerequisites:` from that context's context.yaml (p0202a);
/// read here, with language, so it reaches the early EnsurePrerequisites step. Null = no install.</param>
/// <param name="ToolchainImage">p0265: `stack.image:` — the exact toolchain Docker image named
/// by the LLM. When set (and from a trusted registry) it wins over the language→image table,
/// so any framework/version works without a table row. Null = fall back to the language table.</param>
/// <param name="Resources">p0268: `stack.resources:` — the raw (unparsed) LLM-authored k8s
/// CPU/memory block for this context's sandbox. Validated + applied by SandboxResourceResolver
/// as a layer between the operator override and the global default. Null = use that default.</param>
/// <param name="Purpose">p0331: `meta.purpose:` — what this context is for, in the
/// operator/LLM's own words. Read by the ScopeRepos classifier so it can map a ticket to
/// affected repos from metadata alone, before any checkout or sandbox exists.</param>
public sealed record RemoteContextDiscovery(
    string ContextName, string Workdir, string? Language, string? Prerequisites = null,
    string? ToolchainImage = null, ContextYamlStackResources? Resources = null,
    string? Purpose = null);
