namespace AgentSmith.Contracts.Models;

/// <summary>
/// p0161d: one component surfaced by the read-only BootstrapDiscover round.
/// A component is independently deployable or independently callable, proved
/// by an entrypoint or deploy artefact. A consumed library without either
/// is NOT a component.
///
/// Produced by BootstrapDiscoverHandler from the LLM's structured response;
/// consumed by BootstrapDispatchHandler to fan out one BootstrapRound per
/// (repo, component) — each round writes
/// <c>.agentsmith/contexts/&lt;Name&gt;/{context.yaml,coding-principles.md}</c>.
/// </summary>
/// <param name="Name">Context-directory slug under <c>.agentsmith/contexts/</c>. Operator-readable, lowercase, no slashes.</param>
/// <param name="Workdir">Repo-relative working directory. "." for single-component repos; sub-tree path (e.g. "server", "client") for monorepo components.</param>
/// <param name="Language">Free-form language slug (the LLM owns the vocabulary; see p0155). Drives bootstrap-skill activation via the <c>project_language</c> concept.</param>
/// <param name="Evidence">The file path the LLM cited as proof this component is deployable or callable (entrypoint or deploy artefact).</param>
public sealed record DiscoveredComponent(
    string Name,
    string Workdir,
    string Language,
    string Evidence);
