using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// Discovers contexts on a remote repo (pre-sandbox) so PipelineSandboxCoordinator
/// can fan out one sandbox per context with the right toolchain image per
/// discovery (p0161).
/// </summary>
public interface ISandboxLanguageResolver
{
    Task<IReadOnlyList<RemoteContextDiscovery>> ResolveAllAsync(
        RepoConnection source, CancellationToken cancellationToken);

    /// <summary>
    /// 2026-08-31-f634: the same discovery, reported instead of resolved — the contexts
    /// as read, or the reason the contexts directory could not be listed at all.
    /// <see cref="ResolveAllAsync"/> is built on this and flattens both empty answers
    /// into the synthetic default, because a run needs a sandbox either way. A caller
    /// that tells an operator what to fix asks HERE: "nothing is declared" and "your
    /// credential cannot read this repository" are opposite instructions.
    /// </summary>
    Task<RemoteContextListing> ListContextsAsync(
        RepoConnection source, CancellationToken cancellationToken);

    /// <summary>
    /// p0261: resolve a SINGLE named context (.agentsmith/contexts/&lt;name&gt;),
    /// bypassing discovery and the synthetic-default fallback. Used by the CLI
    /// `--context NAME` flag so a monorepo source whose real contexts discovery
    /// would otherwise collapse to "default" can target one of them directly.
    /// Reads that context's context.yaml for the toolchain; falls back to a
    /// named synthetic (workdir ".", generic image) if it can't be read/parsed,
    /// so the bootstrap probe still hits contexts/&lt;name&gt;/.
    /// </summary>
    Task<IReadOnlyList<RemoteContextDiscovery>> ResolveContextAsync(
        RepoConnection source, string contextName, CancellationToken cancellationToken);
}
